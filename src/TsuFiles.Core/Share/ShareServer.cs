// TsuFiles (ADR-0004 S1, 17/11/2026): gửi và nhận tệp với điện thoại hay máy khác trong cùng mạng nội bộ. Máy tính
// phục vụ một trang web nhỏ; điện thoại (kể cả iPhone 8 với Safari 16) quét mã QR là mở được, không phải cài gì.
//
// Bảo mật: chỉ nhận kết nối từ địa chỉ trong mạng nội bộ (LocalNetwork.IsLocal); mọi đường dẫn mang mã phiên 128 bit
// ngẫu nhiên (so sánh thời gian hằng); thiết bị mới phải được người dùng duyệt; phiên tự tắt khi không dùng. HTTP/1.1
// tối giản tự viết thay HttpListener (trên Windows HttpListener đi qua http.sys, nghe địa chỉ LAN cần quyền quản trị):
// chỉ GET và POST có Content-Length, không chunked, không keep-alive, đầu yêu cầu giới hạn 8 KB. Tệp tải lên gửi
// nguyên thân (mỗi tệp một POST), không có bộ phân tích multipart. Tệp nhận ghi vào tệp tạm rồi mới đổi tên, không
// bao giờ ghi đè, chặn đuôi nguy hiểm (WebFileNames.IsBlocked) và gắn Mark-of-the-Web.

using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TsuFiles.Core.Platform;
using TsuFiles.Core.Web;

namespace TsuFiles.Core.Share;

/// <summary>Thiết bị xin kết nối, đưa cho người dùng duyệt.</summary>
public sealed record ShareDevice(IPAddress Address, string? UserAgent);

/// <summary>Một tệp đã nhận xong.</summary>
public sealed record ShareReceived(string Path, long Length, ShareDevice From);

public sealed class ShareOptions
{
    /// <summary>Thư mục lưu tệp nhận.</summary>
    public required string ReceiveFolder { get; init; }

    /// <summary>Tệp máy tính cho điện thoại tải về.</summary>
    public IReadOnlyList<string> SharedFiles { get; init; } = [];

    /// <summary>Cổng nghe; 0 để hệ điều hành chọn.</summary>
    public int Port { get; init; }

    /// <summary>Dung lượng tối đa một tệp gửi lên.</summary>
    public long MaxUploadBytes { get; init; } = 4L << 30;

    /// <summary>Không có yêu cầu nào trong khoảng này thì phiên tự tắt.</summary>
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Hỏi người dùng có cho thiết bị kết nối không; null = cho mọi thiết bị trong mạng nội bộ (chỉ dùng cho test).</summary>
    public Func<ShareDevice, CancellationToken, Task<bool>>? Approve { get; init; }
}

public sealed class ShareServer : IAsyncDisposable
{
    public const int MaxHeadBytes = 8 * 1024;
    private static readonly TimeSpan HeadTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ApproveTimeout = TimeSpan.FromMinutes(2);

    private readonly ShareOptions _options;
    private readonly ILogger _log;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly byte[] _token = RandomNumberGenerator.GetBytes(16);
    private readonly Dictionary<IPAddress, Task<bool>> _devices = [];
    private readonly Lock _gate = new();
    private long _lastActivity = Environment.TickCount64;
    private Task? _accept;
    private Task? _idle;
    private int _disposed;

    public ShareServer(ShareOptions options, ILogger? log = null)
    {
        _options = options;
        _log = log ?? NullLogger.Instance;
        _listener = new TcpListener(IPAddress.Any, options.Port);
        Token = Convert.ToHexStringLower(_token);
    }

    /// <summary>Mã phiên (32 ký tự hex) nằm trong mọi đường dẫn.</summary>
    public string Token { get; }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public bool IsRunning => _accept is { IsCompleted: false };

    public event Action<ShareReceived>? Received;

    /// <summary>Phiên đã dừng (người dùng tắt, hết giờ, hay lỗi).</summary>
    public event Action? Stopped;

    public void Start()
    {
        _listener.Start();
        _accept = AcceptLoopAsync(_stop.Token);
        _idle = IdleLoopAsync(_stop.Token);
        _log.LogInformation("Share started on port {Port}, receiving into {Folder}", Port, _options.ReceiveFolder);
    }

    /// <summary>Địa chỉ trang chia sẻ cho từng địa chỉ IPv4 mạng nội bộ của máy (thứ tự: Wi-Fi/Ethernet đang bật).</summary>
    public IReadOnlyList<Uri> Links() =>
        ShareAddresses.LocalIPv4().Select(a => new Uri($"http://{a}:{Port}/s/{Token}/")).ToList();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;
        await _stop.CancelAsync();
        _listener.Stop();
        try
        {
            if (_accept is not null)
                await _accept;
        }
        catch (OperationCanceledException)
        {
        }
        _stop.Dispose();
        _log.LogInformation("Share stopped");
        Stopped?.Invoke();
    }

    private async Task IdleLoopAsync(CancellationToken cancellationToken)
    {
        var tick = TimeSpan.FromSeconds(Math.Clamp(_options.IdleTimeout.TotalSeconds / 4, 0.05, 30));
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(tick, cancellationToken);
                if (Environment.TickCount64 - Interlocked.Read(ref _lastActivity) > _options.IdleTimeout.TotalMilliseconds)
                {
                    _log.LogInformation("Share idle for {Minutes} min, stopping", _options.IdleTimeout.TotalMinutes);
                    _ = Task.Run(async () => await DisposeAsync(), CancellationToken.None);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }
            _ = Task.Run(() => ServeAsync(client, cancellationToken), CancellationToken.None);
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var _ = client;
        var remote = ((IPEndPoint)client.Client.RemoteEndPoint!).Address;
        if (remote.IsIPv4MappedToIPv6)
            remote = remote.MapToIPv4();
        if (!LocalNetwork.IsLocal(remote))
        {
            _log.LogWarning("Share refused {Address}: not on the local network", remote);
            return;
        }

        var stream = client.GetStream();
        try
        {
            using var headTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            headTimeout.CancelAfter(HeadTimeout);
            var (request, leftover) = await ShareHttp.ReadRequestAsync(stream, headTimeout.Token);
            if (request is null)
            {
                await ShareHttp.WriteTextAsync(stream, 400, "Bad Request", cancellationToken);
                return;
            }
            Interlocked.Exchange(ref _lastActivity, Environment.TickCount64);
            await HandleAsync(stream, request, leftover, new ShareDevice(remote, request.Header("user-agent")), cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or SocketException)
        {
            _log.LogDebug(ex, "Share connection from {Address} ended early", remote);
        }
    }

    private async Task HandleAsync(Stream stream, ShareRequest request, ReadOnlyMemory<byte> leftover, ShareDevice device,
        CancellationToken cancellationToken)
    {
        // /s/<token>/ , /s/<token>/app.js , /s/<token>/f/<n> , /s/<token>/u?name=<tên>
        var path = request.Path;
        var prefix = "/s/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal) || path.Length < prefix.Length + 32
            || !TokenMatches(path.AsSpan(prefix.Length, 32)))
        {
            await ShareHttp.WriteTextAsync(stream, 404, "Not Found", cancellationToken);
            return;
        }
        var rest = path[(prefix.Length + 32)..];

        if (!await ApprovedAsync(device, cancellationToken))
        {
            await ShareHttp.WriteTextAsync(stream, 403, "Máy tính đã từ chối thiết bị này.", cancellationToken);
            return;
        }

        switch (request.Method, rest)
        {
            case ("GET", "/" or ""):
                await ShareHttp.WriteAsync(stream, 200, "text/html; charset=utf-8",
                    Encoding.UTF8.GetBytes(SharePage.Html(_options.SharedFiles.Select(Path.GetFileName).ToList()!, _options.MaxUploadBytes)),
                    cancellationToken);
                return;
            case ("GET", "/app.js"):
                await ShareHttp.WriteAsync(stream, 200, "text/javascript; charset=utf-8", Encoding.UTF8.GetBytes(SharePage.Script),
                    cancellationToken);
                return;
            case ("GET", _) when rest.StartsWith("/f/", StringComparison.Ordinal):
                await SendFileAsync(stream, rest[3..], cancellationToken);
                return;
            case ("POST", "/u"):
                await ReceiveAsync(stream, request, leftover, device, cancellationToken);
                return;
            default:
                await ShareHttp.WriteTextAsync(stream, 404, "Not Found", cancellationToken);
                return;
        }
    }

    private bool TokenMatches(ReadOnlySpan<char> hex)
    {
        Span<byte> given = stackalloc byte[16];
        return Convert.FromHexString(hex, given, out _, out var written) == OperationStatus.Done && written == 16
               && CryptographicOperations.FixedTimeEquals(given, _token);
    }

    private Task<bool> ApprovedAsync(ShareDevice device, CancellationToken cancellationToken)
    {
        if (_options.Approve is null)
            return Task.FromResult(true);
        lock (_gate)
        {
            if (!_devices.TryGetValue(device.Address, out var decision))
            {
                _log.LogInformation("Share: {Address} asks to connect", device.Address);
                decision = AskAsync(device, cancellationToken);
                _devices[device.Address] = decision;
            }
            return decision;
        }
    }

    private async Task<bool> AskAsync(ShareDevice device, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ApproveTimeout);
        try
        {
            return await _options.Approve!(device, timeout.Token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task SendFileAsync(Stream stream, string index, CancellationToken cancellationToken)
    {
        if (!int.TryParse(index, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n)
            || n < 0 || n >= _options.SharedFiles.Count || !File.Exists(_options.SharedFiles[n]))
        {
            await ShareHttp.WriteTextAsync(stream, 404, "Not Found", cancellationToken);
            return;
        }
        var file = _options.SharedFiles[n];
        await using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true);
        await ShareHttp.WriteFileHeadAsync(stream, Path.GetFileName(file), source.Length, cancellationToken);
        await CopyAsync(source, stream, source.Length, cancellationToken);
        _log.LogInformation("Share sent {Name} ({Length} bytes)", Path.GetFileName(file), source.Length);
    }

    private async Task ReceiveAsync(Stream stream, ShareRequest request, ReadOnlyMemory<byte> leftover, ShareDevice device,
        CancellationToken cancellationToken)
    {
        if (request.Header("transfer-encoding") is not null || request.ContentLength is not { } length)
        {
            await ShareHttp.WriteTextAsync(stream, 411, "Length Required", cancellationToken);
            return;
        }
        if (length > _options.MaxUploadBytes)
        {
            await ShareHttp.WriteTextAsync(stream, 413, "Tệp quá lớn.", cancellationToken);
            return;
        }
        var name = WebFileNames.Sanitize(request.Query("name"), "tep-nhan");
        if (WebFileNames.IsBlocked(name))
        {
            await ShareHttp.WriteTextAsync(stream, 415, "Loại tệp này bị chặn vì có thể chạy mã.", cancellationToken);
            return;
        }

        Directory.CreateDirectory(_options.ReceiveFolder);
        var part = Path.Combine(_options.ReceiveFolder, $".tsufiles-{Guid.NewGuid():N}.part");
        try
        {
            await using (var target = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
            {
                var first = leftover.Length > length ? leftover[..(int)length] : leftover;
                await target.WriteAsync(first, cancellationToken);
                await CopyAsync(stream, target, length - first.Length, cancellationToken);
            }
            var saved = WebFileNames.MoveUnique(part, _options.ReceiveFolder, name);
            MarkOfTheWeb.TryWrite(saved, MarkOfTheWeb.Internet(), _log);
            _log.LogInformation("Share received {Name} ({Length} bytes) from {Address}", Path.GetFileName(saved), length, device.Address);
            // Tell listeners before answering, so the sender only sees 200 once the file is announced.
            // A failing listener must not turn a saved upload into an error for the phone.
            try
            {
                Received?.Invoke(new ShareReceived(saved, length, device));
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Share Received handler failed for {Name}", Path.GetFileName(saved));
            }
            await ShareHttp.WriteTextAsync(stream, 200, Path.GetFileName(saved), cancellationToken);
        }
        finally
        {
            if (File.Exists(part))
                File.Delete(part);
        }
    }

    /// <summary>Chép đúng <paramref name="length"/> byte; thiếu (kết nối đứt) thì ném IOException.</summary>
    private async Task CopyAsync(Stream from, Stream to, long length, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(1 << 16);
        try
        {
            while (length > 0)
            {
                var read = await from.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, length)), cancellationToken);
                if (read == 0)
                    throw new IOException("Connection closed before the whole file arrived.");
                await to.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                length -= read;
                Interlocked.Exchange(ref _lastActivity, Environment.TickCount64);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
