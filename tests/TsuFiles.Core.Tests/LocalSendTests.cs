// ADR-0004 S3: giao thức LocalSend v2 - hai ShareServer thật nói chuyện với nhau trên loopback (127.0.0.1, máy thứ hai
// ở 127.0.0.2), không cần mạng. Multicast thật chỉ kiểm khi môi trường cho phép (không thì bỏ qua).

using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using TsuFiles.Core.Share;
using TsuFiles.Core.Share.LocalSend;

namespace TsuFiles.Core.Tests;

public sealed class LocalSendTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ShareServer Start(string folder, Func<ShareDevice, CancellationToken, Task<bool>>? approve = null, string alias = "Máy B",
        long maxUpload = 4L << 30)
    {
        var server = new ShareServer(new ShareOptions
        {
            ReceiveFolder = folder,
            Approve = approve,
            MaxUploadBytes = maxUpload,
            LocalSend = new LocalSendOptions { Alias = alias, Discover = false },
        });
        server.Start();
        return server;
    }

    private static LocalSendPeer PeerOf(ShareServer server, string address = "127.0.0.1") =>
        new(IPAddress.Parse(address), server.LocalSendSelf!, DateTimeOffset.UtcNow);

    private static readonly LocalSendInfo Sender = new()
    {
        Alias = "Máy A",
        DeviceModel = "TsuFiles",
        DeviceType = "desktop",
        Fingerprint = "a1b2c3",
        Port = 1,
        Protocol = "http",
    };

    private static string Write(string folder, string name, string text)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public async Task Files_sent_to_another_tsufiles_arrive_after_approval()
    {
        using var send = new TempDir();
        using var receive = new TempDir();
        var asked = new List<ShareDevice>();
        await using var server = Start(receive.Path, (d, _) =>
        {
            asked.Add(d);
            return Task.FromResult(true);
        });
        var received = new List<ShareReceived>();
        server.Received += received.Add;

        var files = new[] { Write(send.Path, "báo cáo.txt", "xin chào"), Write(send.Path, "ảnh.jpg", "jpeg") };
        var result = await LocalSendClient.SendAsync(Sender, PeerOf(server), files, cancellationToken: Ct);

        Assert.Equal(LocalSendSendStatus.Sent, result.Status);
        Assert.Equal(2, result.SentFiles.Count);
        Assert.Equal("xin chào", File.ReadAllText(Path.Combine(receive.Path, "báo cáo.txt")));
        Assert.Equal("jpeg", File.ReadAllText(Path.Combine(receive.Path, "ảnh.jpg")));
        Assert.Equal("Máy A", Assert.Single(asked).Alias);
        Assert.All(received, r => Assert.Equal("Máy A", r.From.Alias));
        Assert.Empty(Directory.GetFiles(receive.Path, "*.part"));
        // Máy gửi được ghi vào danh bạ của máy nhận.
        Assert.Contains(server.Discovery!.Peers, p => p.Info.Alias == "Máy A");
    }

    [Fact]
    public async Task A_refused_device_sends_nothing()
    {
        using var send = new TempDir();
        using var receive = new TempDir();
        await using var server = Start(receive.Path, (_, _) => Task.FromResult(false));

        var result = await LocalSendClient.SendAsync(Sender, PeerOf(server), [Write(send.Path, "a.txt", "a")], cancellationToken: Ct);

        Assert.Equal(LocalSendSendStatus.Rejected, result.Status);
        Assert.Empty(Directory.GetFiles(receive.Path));
    }

    [Fact]
    public async Task Executables_and_oversized_files_are_left_out()
    {
        using var send = new TempDir();
        using var receive = new TempDir();
        await using var server = Start(receive.Path, maxUpload: 10);

        var files = new[] { Write(send.Path, "cai-dat.exe", "MZ"), Write(send.Path, "lon.bin", new string('x', 11)), Write(send.Path, "ok.txt", "ok") };
        var result = await LocalSendClient.SendAsync(Sender, PeerOf(server), files, cancellationToken: Ct);

        Assert.Equal(LocalSendSendStatus.Sent, result.Status);
        Assert.Equal(["ok.txt"], Directory.GetFiles(receive.Path).Select(Path.GetFileName));

        var none = await LocalSendClient.SendAsync(Sender, PeerOf(server), [files[0]], cancellationToken: Ct);
        Assert.Equal(LocalSendSendStatus.Rejected, none.Status);
    }

    private static async Task<(string Session, string FileId, string Token)> PrepareAsync(HttpClient http, ShareServer server, long size,
        string? sha256 = null, string name = "x.txt")
    {
        var hash = sha256 is null ? "" : $",\"sha256\":\"{sha256}\"";
        var body = "{\"info\":{\"alias\":\"curl\",\"version\":\"2.1\",\"fingerprint\":\"f1\",\"port\":1,\"protocol\":\"http\"},"
                   + $"\"files\":{{\"f\":{{\"id\":\"f\",\"fileName\":\"{name}\",\"size\":{size},\"fileType\":\"text/plain\"{hash}}}}}}}";
        var response = await http.PostAsync($"http://127.0.0.1:{server.Port}/api/localsend/v2/prepare-upload",
            new StringContent(body, Encoding.UTF8, "application/json"), Ct);
        response.EnsureSuccessStatusCode();
        var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
        return (json.GetProperty("sessionId").GetString()!, "f", json.GetProperty("files").GetProperty("f").GetString()!);
    }

    private static string UploadUrl(ShareServer server, (string Session, string FileId, string Token) p) =>
        $"http://127.0.0.1:{server.Port}/api/localsend/v2/upload?sessionId={p.Session}&fileId={p.FileId}&token={p.Token}";

    [Fact]
    public async Task Upload_tokens_are_checked_and_used_once()
    {
        using var receive = new TempDir();
        await using var server = Start(receive.Path);
        using var http = new HttpClient();
        var p = await PrepareAsync(http, server, 3);

        var wrong = await http.PostAsync(UploadUrl(server, p with { Token = new string('0', 32) }), new ByteArrayContent("abc"u8.ToArray()), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        var ok = await http.PostAsync(UploadUrl(server, p), new ByteArrayContent("abc"u8.ToArray()), Ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var again = await http.PostAsync(UploadUrl(server, p), new ByteArrayContent("abc"u8.ToArray()), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, again.StatusCode);
        Assert.Single(Directory.GetFiles(receive.Path));
    }

    [Fact]
    public async Task A_wrong_checksum_or_length_saves_nothing()
    {
        using var receive = new TempDir();
        await using var server = Start(receive.Path);
        using var http = new HttpClient();

        var bad = await PrepareAsync(http, server, 3, sha256: Convert.ToHexString(SHA256.HashData("xyz"u8)));
        Assert.Equal((HttpStatusCode)422, (await http.PostAsync(UploadUrl(server, bad), new ByteArrayContent("abc"u8.ToArray()), Ct)).StatusCode);

        var good = await PrepareAsync(http, server, 3, sha256: Convert.ToHexString(SHA256.HashData("abc"u8)).ToLowerInvariant());
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsync(UploadUrl(server, good), new ByteArrayContent("abc"u8.ToArray()), Ct)).StatusCode);

        var shortBody = await PrepareAsync(http, server, 5);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await http.PostAsync(UploadUrl(server, shortBody), new ByteArrayContent("abc"u8.ToArray()), Ct)).StatusCode);

        Assert.Equal(["x.txt"], Directory.GetFiles(receive.Path).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("""{"info":null,"files":{"f":{"id":"f","fileName":"x.txt","size":1}}}""")]
    [InlineData("""{"info":{"alias":null,"fingerprint":"f1"},"files":{"f":{"id":"f","fileName":"x.txt","size":1}}}""")]
    [InlineData("""{"info":{"alias":"a","fingerprint":"f1"},"files":{"f":null}}""")]
    [InlineData("""{"info":{"alias":"a","fingerprint":"f1"},"files":{"f":{"id":"f","fileName":null,"size":1}}}""")]
    [InlineData("""{"info":{"alias":"a","fingerprint":"f1"},"files":null}""")]
    public async Task Null_fields_from_a_stranger_are_a_bad_request_not_a_crash(string body)
    {
        using var receive = new TempDir();
        await using var server = Start(receive.Path);
        using var http = new HttpClient();

        var response = await http.PostAsync($"http://127.0.0.1:{server.Port}/api/localsend/v2/prepare-upload",
            new StringContent(body, Encoding.UTF8, "application/json"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var register = await http.PostAsync($"http://127.0.0.1:{server.Port}/api/localsend/v2/register",
            new StringContent("""{"alias":null,"fingerprint":"f1"}""", Encoding.UTF8, "application/json"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);

        // Máy chủ vẫn chạy bình thường sau các yêu cầu sai.
        await PrepareAsync(http, server, 1);
    }

    [Fact]
    public async Task A_flood_of_announcements_opens_only_a_few_connections_back()
    {
        var gate = new TaskCompletionSource();
        var running = 0;
        var peak = 0;
        await using var discovery = new LocalSendDiscovery(Sender, async (_, _) =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref peak, now);
            await gate.Task;
            Interlocked.Decrement(ref running);
            return true;
        });
        var tasks = Enumerable.Range(0, 50).Select(i =>
        {
            var packet = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                Sender with { Fingerprint = $"peer{i}", Announce = true }, LocalSendJsonContext.Default.LocalSendInfo);
            return discovery.HandlePacketAsync(packet, new IPEndPoint(IPAddress.Parse("192.168.1.9"), 53317), Ct);
        }).ToList();
        gate.SetResult();
        await Task.WhenAll(tasks);

        Assert.InRange(peak, 1, LocalSendDiscovery.MaxConcurrentRegisterBacks);
        Assert.Equal(50, discovery.Peers.Count); // vẫn ghi nhận đủ máy, chỉ không trả lời hết
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while ((seen = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, seen) != seen)
        {
        }
    }

    [Fact]
    public async Task A_second_device_waits_while_a_session_runs()
    {
        if (!await CanUseAsync(IPAddress.Parse("127.0.0.2")))
            Assert.Skip("127.0.0.2 is not usable here");
        using var receive = new TempDir();
        await using var server = Start(receive.Path);
        using var first = new HttpClient();
        await PrepareAsync(first, server, 3);

        using var second = new HttpClient(new SocketsHttpHandler { ConnectCallback = BindTo(IPAddress.Parse("127.0.0.2")) });
        var body = """{"info":{"alias":"khác","fingerprint":"f2"},"files":{"g":{"id":"g","fileName":"g.txt","size":1}}}""";
        var response = await second.PostAsync($"http://127.0.0.1:{server.Port}/api/localsend/v2/prepare-upload",
            new StringContent(body, Encoding.UTF8, "application/json"), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_and_announce_fill_both_address_books()
    {
        using var a = new TempDir();
        using var b = new TempDir();
        await using var serverA = Start(a.Path, alias: "Máy A");
        await using var serverB = Start(b.Path, alias: "Máy B");

        // B nghe thấy gói thông báo của A (giả lập multicast) và trả lời A bằng POST /register.
        var announce = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(serverA.LocalSendSelf! with { Announce = true },
            LocalSendJsonContext.Default.LocalSendInfo);
        await serverB.Discovery!.HandlePacketAsync(announce, new IPEndPoint(IPAddress.Loopback, 53317), Ct);

        Assert.Equal("Máy A", Assert.Single(serverB.Discovery.Peers).Info.Alias);
        Assert.Equal("Máy B", Assert.Single(serverA.Discovery!.Peers).Info.Alias);
    }

    [Fact]
    public async Task Packets_from_outside_the_local_network_or_from_itself_are_ignored()
    {
        await using var discovery = new LocalSendDiscovery(Sender);
        var packet = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Sender with { Fingerprint = "other", Announce = false },
            LocalSendJsonContext.Default.LocalSendInfo);

        await discovery.HandlePacketAsync(packet, new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53317), Ct);
        await discovery.HandlePacketAsync(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Sender, LocalSendJsonContext.Default.LocalSendInfo),
            new IPEndPoint(IPAddress.Parse("192.168.1.9"), 53317), Ct);
        await discovery.HandlePacketAsync("{not json"u8.ToArray(), new IPEndPoint(IPAddress.Parse("192.168.1.9"), 53317), Ct);
        Assert.Empty(discovery.Peers);

        await discovery.HandlePacketAsync(packet, new IPEndPoint(IPAddress.Parse("192.168.1.9"), 53317), Ct);
        Assert.Equal(IPAddress.Parse("192.168.1.9"), Assert.Single(discovery.Peers).Address);
    }

    [Fact]
    public void Https_peers_are_trusted_only_by_their_announced_fingerprint()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var cert = new CertificateRequest("CN=LocalSend", key, HashAlgorithmName.SHA256)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var fingerprint = Convert.ToHexString(SHA256.HashData(cert.RawData));

        Assert.True(LocalSendClient.FingerprintMatches(cert, fingerprint));
        Assert.True(LocalSendClient.FingerprintMatches(cert, fingerprint.ToLowerInvariant()));
        Assert.False(LocalSendClient.FingerprintMatches(cert, new string('A', 64)));
        Assert.False(LocalSendClient.FingerprintMatches(cert, "random-http-fingerprint"));
    }

    [Fact]
    public async Task Real_multicast_finds_the_other_device_when_the_network_allows_it()
    {
        var port = 40000 + Random.Shared.Next(20000);
        await using var a = new LocalSendDiscovery(Sender with { Alias = "A", Fingerprint = "fa" }, port: port);
        await using var b = new LocalSendDiscovery(Sender with { Alias = "B", Fingerprint = "fb" }, port: port);
        if (!a.Start() || !b.Start())
            Assert.Skip("UDP multicast is not available here");

        await a.AnnounceAsync(Ct);
        for (var i = 0; i < 30 && b.Peers.Count == 0; i++)
            await Task.Delay(100, Ct);
        if (b.Peers.Count == 0)
            Assert.Skip("Multicast packets do not loop back on this machine");

        Assert.Equal("A", b.Peers[0].Info.Alias);
        // B trả lời bằng gói UDP (không có registerBack) nên A cũng thấy B.
        for (var i = 0; i < 30 && a.Peers.Count == 0; i++)
            await Task.Delay(100, Ct);
        Assert.Equal("B", Assert.Single(a.Peers).Info.Alias);
    }

    private static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> BindTo(IPAddress local) => async (context, token) =>
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            socket.Bind(new IPEndPoint(local, 0));
            await socket.ConnectAsync(context.DnsEndPoint, token);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    };

    private static async Task<bool> CanUseAsync(IPAddress local)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(local, 0));
            await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
