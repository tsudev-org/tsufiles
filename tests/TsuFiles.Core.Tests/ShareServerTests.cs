// ADR-0004 S1: máy chủ chia sẻ trong mạng nội bộ. Chạy thật trên 127.0.0.1 (loopback tính là nội bộ).

using System.Net;
using System.Text;
using TsuFiles.Core.Share;

namespace TsuFiles.Core.Tests;

public sealed class ShareServerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(ShareServer Server, HttpClient Http, string Base)> StartAsync(ShareOptions options)
    {
        var server = new ShareServer(options);
        server.Start();
        var http = new HttpClient();
        return (server, http, $"http://127.0.0.1:{server.Port}/s/{server.Token}/");
    }

    [Fact]
    public async Task Only_the_session_token_opens_the_page()
    {
        using var dir = new TempDir();
        var (server, http, root) = await StartAsync(new ShareOptions { ReceiveFolder = dir.Path });
        await using var _ = server;
        using var __ = http;

        Assert.Equal(32, server.Token.Length);
        var page = await http.GetAsync(root, Ct);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("script-src 'self'", page.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal(HttpStatusCode.NotFound,
            (await http.GetAsync($"http://127.0.0.1:{server.Port}/s/{new string('0', 32)}/", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"http://127.0.0.1:{server.Port}/", Ct)).StatusCode);
    }

    [Fact]
    public async Task An_upload_lands_under_a_safe_name_and_never_overwrites()
    {
        using var dir = new TempDir();
        var (server, http, root) = await StartAsync(new ShareOptions { ReceiveFolder = dir.Path });
        await using var _ = server;
        using var __ = http;
        var received = new List<ShareReceived>();
        server.Received += received.Add;

        for (var i = 0; i < 2; i++)
        {
            var response = await http.PostAsync(root + "u?name=" + Uri.EscapeDataString("../../ảnh chụp.jpg"),
                new ByteArrayContent(Encoding.UTF8.GetBytes("abc")), Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var files = Directory.GetFiles(dir.Path).Select(Path.GetFileName).Order().ToList();
        Assert.Equal(["ảnh chụp (2).jpg", "ảnh chụp.jpg"], files);
        Assert.Equal("abc", File.ReadAllText(Path.Combine(dir.Path, "ảnh chụp.jpg")));
        Assert.Equal(2, received.Count);
        Assert.Empty(Directory.GetFiles(dir.Path, "*.part"));
    }

    [Theory]
    [InlineData("setup.exe", 3, HttpStatusCode.UnsupportedMediaType)]
    [InlineData("big.bin", 11, HttpStatusCode.RequestEntityTooLarge)]
    public async Task Dangerous_or_oversized_uploads_are_refused(string name, int length, HttpStatusCode expected)
    {
        using var dir = new TempDir();
        var (server, http, root) = await StartAsync(new ShareOptions { ReceiveFolder = dir.Path, MaxUploadBytes = 10 });
        await using var _ = server;
        using var __ = http;

        var response = await http.PostAsync(root + "u?name=" + name, new ByteArrayContent(new byte[length]), Ct);

        Assert.Equal(expected, response.StatusCode);
        Assert.Empty(Directory.GetFiles(dir.Path));
    }

    [Fact]
    public async Task A_shared_file_downloads_with_its_vietnamese_name()
    {
        using var dir = new TempDir();
        var file = dir.File("Báo cáo <1>.txt", "nội dung");
        var (server, http, root) = await StartAsync(new ShareOptions { ReceiveFolder = dir.Path, SharedFiles = [file] });
        await using var _ = server;
        using var __ = http;

        var page = await http.GetStringAsync(root, Ct);
        Assert.Contains("Báo cáo &lt;1&gt;.txt", page);
        var response = await http.GetAsync(root + "f/0", Ct);
        Assert.Equal("nội dung", await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Báo cáo <1>.txt", response.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(root + "f/1", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(root + "f/-1", Ct)).StatusCode);
    }

    [Fact]
    public async Task A_refused_device_is_asked_once_and_stays_refused()
    {
        using var dir = new TempDir();
        var asked = 0;
        var (server, http, root) = await StartAsync(new ShareOptions
        {
            ReceiveFolder = dir.Path,
            Approve = (_, _) => { Interlocked.Increment(ref asked); return Task.FromResult(false); },
        });
        await using var _ = server;
        using var __ = http;

        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync(root, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await http.PostAsync(root + "u?name=a.txt", new ByteArrayContent([1]), Ct)).StatusCode);
        Assert.Equal(1, asked);
        Assert.Empty(Directory.GetFiles(dir.Path));
    }

    [Fact]
    public async Task An_idle_session_stops_by_itself()
    {
        using var dir = new TempDir();
        var server = new ShareServer(new ShareOptions { ReceiveFolder = dir.Path, IdleTimeout = TimeSpan.FromMilliseconds(200) });
        var stopped = new TaskCompletionSource();
        server.Stopped += () => stopped.TrySetResult();
        server.Start();

        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Assert.False(server.IsRunning);
        await server.DisposeAsync();
    }

    [Theory]
    [InlineData("GET /s/x HTTP/1.1\r\nContent-Length: 1\r\nContent-Length: 2")] // smuggling
    [InlineData("GET /s/x HTTP/1.1\r\nHost : a")] // space before colon
    [InlineData("GET /s/../etc HTTP/1.1")]
    [InlineData("GET /s/%2e%2e/etc HTTP/1.1")]
    [InlineData("PUT /s/x HTTP/1.1")]
    [InlineData("GET s/x HTTP/1.1")]
    [InlineData("GET /s/x HTTP/2")]
    public void Malformed_or_dangerous_requests_are_rejected(string head) => Assert.Null(ShareHttp.Parse(head));

    [Fact]
    public void A_request_parses_path_query_and_length()
    {
        var request = ShareHttp.Parse("POST /s/abc/u?name=%E1%BA%A3nh+1.jpg HTTP/1.1\r\nContent-Length: 42\r\nUser-Agent: iPhone");

        Assert.NotNull(request);
        Assert.Equal("/s/abc/u", request.Path);
        Assert.Equal("ảnh 1.jpg", request.Query("name"));
        Assert.Equal(42, request.ContentLength);
        Assert.Equal("iPhone", request.Header("User-Agent"));
    }

    [Theory]
    [InlineData("192.168.1.5", true)]
    [InlineData("10.0.0.2", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("169.254.3.4", false)]
    [InlineData("8.8.8.8", false)]
    public void Only_reachable_lan_addresses_go_into_the_qr_code(string address, bool usable) =>
        Assert.Equal(usable, ShareAddresses.Usable(IPAddress.Parse(address)));
}
