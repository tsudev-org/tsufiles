// ShareServer nói giao thức LocalSend v2 (ADR-0004 S3): app LocalSend trên điện thoại (iOS 15+ - gồm iPhone 8, Android)
// hay máy tính, và TsuFiles khác, thấy máy này trong danh sách và gửi tệp tới được.
//
// Tuyến /api/localsend/v2/* không có mã phiên trong URL (giao thức không có) nên an toàn dựa vào: chỉ địa chỉ mạng
// nội bộ; /prepare-upload phải qua bước người dùng duyệt thiết bị như trang điện thoại; mỗi tệp một token ngẫu nhiên
// 128 bit, chỉ dùng một lần, chỉ từ đúng địa chỉ đã xin; một phiên nhận tại một thời điểm (409 cho máy khác); thân
// JSON tối đa 1 MB; kích thước tệp phải khớp đúng số đã khai; có sha256 thì kiểm (422 nếu sai). Tệp nhận đi qua cùng
// đường lưu với trang điện thoại (tên làm sạch, chặn đuôi chạy được mã, không ghi đè, Mark-of-the-Web).

using System.Buffers;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TsuFiles.Core.Platform;
using TsuFiles.Core.Share.LocalSend;
using TsuFiles.Core.Web;

namespace TsuFiles.Core.Share;

public sealed partial class ShareServer
{
    private static readonly TimeSpan LocalSendSessionIdle = TimeSpan.FromMinutes(5);
    private LocalSendSession? _session;

    private sealed class LocalSendSession
    {
        public required string Id { get; init; }
        public required IPAddress From { get; init; }
        public required ShareDevice Device { get; init; }
        public required Dictionary<string, (LocalSendFile File, byte[] Token)> Pending { get; init; }
        public long LastActivity = Environment.TickCount64;
    }

    private void StartLocalSend()
    {
        if (_options.LocalSend is not { } ls)
            return;
        LocalSendSelf = new LocalSendInfo
        {
            Alias = ls.Alias,
            DeviceModel = "TsuFiles",
            DeviceType = ls.DeviceType,
            Fingerprint = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)),
            Port = Port,
            Protocol = "http",
            Download = false,
        };
        var self = LocalSendSelf;
        Discovery = new LocalSendDiscovery(self,
            async (peer, ct) => await LocalSendClient.RegisterAsync(self, peer, ct) is not null, _log, ls.DiscoveryPort);
        if (ls.Discover && Discovery.Start())
            _ = Discovery.AnnounceAsync(_stop.Token);
    }

    private async Task HandleLocalSendAsync(Stream stream, ShareRequest request, ReadOnlyMemory<byte> leftover, ShareDevice device,
        string route, CancellationToken cancellationToken)
    {
        switch (request.Method, route)
        {
            case ("POST", "register"):
            {
                var info = await ReadJsonAsync(stream, request, leftover, LocalSendJsonContext.Default.LocalSendInfo, cancellationToken);
                if (info is null)
                {
                    await ShareHttp.WriteTextAsync(stream, 400, "Invalid body", cancellationToken);
                    return;
                }
                Discovery?.Observe(device.Address, info);
                await WriteJsonAsync(stream, LocalSendSelf! with { Announce = null }, cancellationToken);
                return;
            }
            case ("GET", "info"):
                await WriteJsonAsync(stream, LocalSendSelf! with { Port = null, Protocol = null, Announce = null }, cancellationToken);
                return;
            case ("POST", "prepare-upload"):
                await PrepareUploadAsync(stream, request, leftover, device, cancellationToken);
                return;
            case ("POST", "upload"):
                await LocalSendUploadAsync(stream, request, leftover, device, cancellationToken);
                return;
            case ("POST", "cancel"):
                lock (_gate)
                {
                    if (_session is { } s && s.Id == request.Query("sessionId") && s.From.Equals(device.Address))
                        _session = null;
                }
                await ShareHttp.WriteAsync(stream, 200, "text/plain; charset=utf-8", [], cancellationToken);
                return;
            default:
                await ShareHttp.WriteTextAsync(stream, 404, "Not Found", cancellationToken);
                return;
        }
    }

    private async Task PrepareUploadAsync(Stream stream, ShareRequest request, ReadOnlyMemory<byte> leftover, ShareDevice device,
        CancellationToken cancellationToken)
    {
        // TsuFiles không đặt PIN (?pin= bỏ qua): người dùng duyệt từng thiết bị trên máy tính thay cho PIN.
        var body = await ReadJsonAsync(stream, request, leftover, LocalSendJsonContext.Default.LocalSendPrepareUpload, cancellationToken);
        // RespectNullableAnnotations không kiểm phần tử của từ điển: "files":{"a":null} phải chặn tay.
        if (body is null || body.Files.Count is 0 or > LocalSendProtocol.MaxFilesPerSession || body.Files.Values.Any(f => f is null))
        {
            await ShareHttp.WriteTextAsync(stream, 400, "Invalid body", cancellationToken);
            return;
        }
        bool blocked;
        lock (_gate)
        {
            blocked = _session is { } busy && !busy.From.Equals(device.Address)
                      && Environment.TickCount64 - Interlocked.Read(ref busy.LastActivity) < LocalSendSessionIdle.TotalMilliseconds;
        }
        if (blocked)
        {
            await ShareHttp.WriteTextAsync(stream, 409, "Blocked by another session", cancellationToken);
            return;
        }

        var trimmed = body.Info.Alias.Trim();
        var alias = trimmed.Length == 0 ? null : trimmed[..Math.Min(64, trimmed.Length)];
        var named = device with { Alias = alias, UserAgent = $"LocalSend {body.Info.DeviceModel ?? body.Info.DeviceType ?? ""}".TrimEnd() };
        Discovery?.Observe(device.Address, body.Info);
        if (!await ApprovedAsync(named, cancellationToken))
        {
            await ShareHttp.WriteTextAsync(stream, 403, "Rejected", cancellationToken);
            return;
        }

        // Nhận một phần: bỏ tệp quá lớn, loại chạy được mã, id/tên rỗng. Máy gửi chỉ gửi các tệp có trong câu trả lời.
        var pending = new Dictionary<string, (LocalSendFile, byte[])>(StringComparer.Ordinal);
        var answer = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (id, file) in body.Files)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 128 || file.Size < 0 || file.Size > _options.MaxUploadBytes
                || WebFileNames.IsBlocked(WebFileNames.Sanitize(file.FileName, "tep-nhan")))
            {
                _log.LogInformation("Share (LocalSend): skipped {Name} from {Address}", file.FileName, device.Address);
                continue;
            }
            var token = RandomNumberGenerator.GetBytes(16);
            pending[id] = (file with { Id = id }, token);
            answer[id] = Convert.ToHexStringLower(token);
        }
        if (pending.Count == 0)
        {
            await ShareHttp.WriteTextAsync(stream, 403, "Rejected", cancellationToken);
            return;
        }
        var session = new LocalSendSession
        {
            Id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)),
            From = device.Address,
            Device = named,
            Pending = pending,
        };
        lock (_gate)
            _session = session;
        await WriteJsonAsync(stream, new LocalSendPrepareResponse { SessionId = session.Id, Files = answer }, cancellationToken);
    }

    private async Task LocalSendUploadAsync(Stream stream, ShareRequest request, ReadOnlyMemory<byte> leftover, ShareDevice device,
        CancellationToken cancellationToken)
    {
        var sessionId = request.Query("sessionId");
        var fileId = request.Query("fileId");
        var token = request.Query("token");
        if (sessionId is null || fileId is null || token is null)
        {
            await ShareHttp.WriteTextAsync(stream, 400, "Missing parameters", cancellationToken);
            return;
        }

        LocalSendSession? session;
        LocalSendFile file;
        lock (_gate)
        {
            session = _session;
            if (session is null || session.Id != sessionId || !session.From.Equals(device.Address)
                || !session.Pending.TryGetValue(fileId, out var entry) || !TokenEquals(token, entry.Token))
            {
                session = null;
                file = null!;
            }
            else
            {
                file = entry.File;
                session.Pending.Remove(fileId); // token dùng một lần
                Interlocked.Exchange(ref session.LastActivity, Environment.TickCount64);
            }
        }
        if (session is null)
        {
            await ShareHttp.WriteTextAsync(stream, 403, "Invalid token or IP address", cancellationToken);
            return;
        }
        if (request.Header("transfer-encoding") is not null || request.ContentLength != file.Size)
        {
            await ShareHttp.WriteTextAsync(stream, 400, "Content-Length must match the announced size", cancellationToken);
            return;
        }

        var name = WebFileNames.Sanitize(file.FileName, "tep-nhan");
        Directory.CreateDirectory(_options.ReceiveFolder);
        var part = Path.Combine(_options.ReceiveFolder, $".tsufiles-{Guid.NewGuid():N}.part");
        try
        {
            byte[] hash;
            await using (var target = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
            using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var first = leftover.Length > file.Size ? leftover[..(int)file.Size] : leftover;
                sha.AppendData(first.Span);
                await target.WriteAsync(first, cancellationToken);
                await CopyHashedAsync(stream, target, sha, file.Size - first.Length, session, cancellationToken);
                hash = sha.GetHashAndReset();
            }
            if (file.Sha256 is { Length: > 0 } expected && !string.Equals(Convert.ToHexString(hash), expected, StringComparison.OrdinalIgnoreCase))
            {
                await ShareHttp.WriteTextAsync(stream, 422, "Checksum mismatch (sha256)", cancellationToken);
                return;
            }
            var saved = WebFileNames.MoveUnique(part, _options.ReceiveFolder, name);
            MarkOfTheWeb.TryWrite(saved, MarkOfTheWeb.Internet(), _log);
            _log.LogInformation("Share (LocalSend) received {Name} ({Length} bytes) from {Address}", Path.GetFileName(saved), file.Size,
                device.Address);
            try
            {
                Received?.Invoke(new ShareReceived(saved, file.Size, session.Device));
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Share Received handler failed for {Name}", Path.GetFileName(saved));
            }
            lock (_gate)
            {
                if (ReferenceEquals(_session, session) && session.Pending.Count == 0)
                    _session = null;
            }
            await ShareHttp.WriteAsync(stream, 200, "text/plain; charset=utf-8", [], cancellationToken);
        }
        finally
        {
            if (File.Exists(part))
                File.Delete(part);
        }
    }

    private static bool TokenEquals(string given, byte[] expected)
    {
        Span<byte> bytes = stackalloc byte[16];
        return given.Length == 32
               && Convert.FromHexString(given, bytes, out _, out var written) == OperationStatus.Done && written == 16
               && CryptographicOperations.FixedTimeEquals(bytes, expected);
    }

    private async Task CopyHashedAsync(Stream from, Stream to, IncrementalHash sha, long length, LocalSendSession session,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(1 << 16);
        try
        {
            while (length > 0)
            {
                var read = await from.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, length)), cancellationToken);
                if (read == 0)
                    throw new IOException("Connection closed before the whole file arrived.");
                sha.AppendData(buffer, 0, read);
                await to.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                length -= read;
                Interlocked.Exchange(ref _lastActivity, Environment.TickCount64);
                Interlocked.Exchange(ref session.LastActivity, Environment.TickCount64);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<T?> ReadJsonAsync<T>(Stream stream, ShareRequest request, ReadOnlyMemory<byte> leftover,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type, CancellationToken cancellationToken) where T : class
    {
        if (request.Header("transfer-encoding") is not null || request.ContentLength is not { } length
            || length > LocalSendProtocol.MaxJsonBytes)
            return null;
        var body = new byte[length];
        var have = (int)Math.Min(leftover.Length, length);
        leftover[..have].CopyTo(body);
        await stream.ReadExactlyAsync(body.AsMemory(have), cancellationToken);
        try
        {
            return JsonSerializer.Deserialize(body, type);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Task WriteJsonAsync<T>(Stream stream, T value, CancellationToken cancellationToken) where T : class
    {
        var type = (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)LocalSendJsonContext.Default.GetTypeInfo(typeof(T))!;
        return ShareHttp.WriteAsync(stream, 200, "application/json", JsonSerializer.SerializeToUtf8Bytes(value, type), cancellationToken);
    }
}
