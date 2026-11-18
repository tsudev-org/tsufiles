// Gửi tệp tới một máy LocalSend/TsuFiles (ADR-0004 S3): POST /prepare-upload (máy nhận duyệt), rồi POST /upload từng
// tệp. Máy nhận dùng HTTPS (mặc định của app LocalSend) có chứng thư tự ký: chỉ chấp nhận đúng chứng thư có SHA-256
// trùng vân tay máy đó đã công bố khi được tìm thấy (mô hình tin cậy của giao thức), không tắt kiểm chứng thư bừa.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace TsuFiles.Core.Share.LocalSend;

public enum LocalSendSendStatus
{
    Sent,
    NothingToSend,
    Rejected,
    PinRequired,
    Busy,
    Failed,
}

public sealed record LocalSendSendResult(LocalSendSendStatus Status, IReadOnlyList<string> SentFiles, string? Error = null);

public static class LocalSendClient
{
    private static readonly TimeSpan PrepareTimeout = TimeSpan.FromMinutes(2); // người bên kia cần thời gian bấm "Cho phép"

    /// <summary>Báo cho <paramref name="peer"/> biết máy này (trả lời một thông báo multicast). Trả về thông tin máy kia.</summary>
    public static async Task<LocalSendInfo?> RegisterAsync(LocalSendInfo self, LocalSendPeer peer, CancellationToken cancellationToken)
    {
        using var http = CreateHttp(peer, TimeSpan.FromSeconds(5));
        using var response = await http.PostAsync(new Uri(peer.BaseUri, "register"), Json(self with { Announce = null }), cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync(body, LocalSendJsonContext.Default.LocalSendInfo, cancellationToken);
    }

    /// <summary>Gửi <paramref name="files"/>; <paramref name="progress"/> nhận (tên tệp, số byte đã gửi của tệp đó).</summary>
    public static async Task<LocalSendSendResult> SendAsync(LocalSendInfo self, LocalSendPeer peer, IReadOnlyList<string> files,
        IProgress<(string File, long Sent)>? progress = null, CancellationToken cancellationToken = default)
    {
        var byId = new Dictionary<string, string>(StringComparer.Ordinal);
        var meta = new Dictionary<string, LocalSendFile>(StringComparer.Ordinal);
        foreach (var path in files)
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                return new LocalSendSendResult(LocalSendSendStatus.Failed, [], $"File not found: {path}");
            var id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
            byId[id] = info.FullName;
            meta[id] = new LocalSendFile { Id = id, FileName = info.Name, Size = info.Length, FileType = MimeOf(info.Name) };
        }

        using var http = CreateHttp(peer, Timeout.InfiniteTimeSpan);
        LocalSendPrepareResponse? prepared;
        try
        {
            using var prepareTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            prepareTimeout.CancelAfter(PrepareTimeout);
            using var response = await http.PostAsync(new Uri(peer.BaseUri, "prepare-upload"),
                Json(new LocalSendPrepareUpload { Info = self with { Announce = null }, Files = meta }), prepareTimeout.Token);
            switch (response.StatusCode)
            {
                case HttpStatusCode.NoContent:
                    return new LocalSendSendResult(LocalSendSendStatus.NothingToSend, []);
                case HttpStatusCode.Forbidden:
                    return new LocalSendSendResult(LocalSendSendStatus.Rejected, []);
                case HttpStatusCode.Unauthorized:
                    return new LocalSendSendResult(LocalSendSendStatus.PinRequired, []);
                case HttpStatusCode.Conflict or HttpStatusCode.TooManyRequests:
                    return new LocalSendSendResult(LocalSendSendStatus.Busy, []);
            }
            if (!response.IsSuccessStatusCode)
                return new LocalSendSendResult(LocalSendSendStatus.Failed, [], $"HTTP {(int)response.StatusCode}");
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            prepared = await JsonSerializer.DeserializeAsync(body, LocalSendJsonContext.Default.LocalSendPrepareResponse, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException
                                       || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return new LocalSendSendResult(LocalSendSendStatus.Failed, [], ex.Message);
        }
        if (prepared is null || string.IsNullOrEmpty(prepared.SessionId))
            return new LocalSendSendResult(LocalSendSendStatus.Failed, [], "Invalid prepare-upload response");

        var sent = new List<string>();
        try
        {
            foreach (var (id, token) in prepared.Files)
            {
                if (!byId.TryGetValue(id, out var path))
                    continue; // máy nhận chỉ trả các tệp nó nhận; id lạ thì bỏ
                var url = new Uri(peer.BaseUri, $"upload?sessionId={Uri.EscapeDataString(prepared.SessionId)}" +
                                               $"&fileId={Uri.EscapeDataString(id)}&token={Uri.EscapeDataString(token)}");
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true);
                var name = Path.GetFileName(path);
                using var content = new ProgressContent(stream, s => progress?.Report((name, s)));
                content.Headers.ContentType = new MediaTypeHeaderValue(meta[id].FileType);
                using var response = await http.PostAsync(url, content, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    await CancelAsync(http, peer, prepared.SessionId);
                    return new LocalSendSendResult(LocalSendSendStatus.Failed, sent, $"{name}: HTTP {(int)response.StatusCode}");
                }
                sent.Add(path);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
        {
            await CancelAsync(http, peer, prepared.SessionId);
            if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                throw;
            return new LocalSendSendResult(LocalSendSendStatus.Failed, sent, ex.Message);
        }
        return new LocalSendSendResult(sent.Count > 0 ? LocalSendSendStatus.Sent : LocalSendSendStatus.NothingToSend, sent);
    }

    private static async Task CancelAsync(HttpClient http, LocalSendPeer peer, string sessionId)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var _ = await http.PostAsync(new Uri(peer.BaseUri, $"cancel?sessionId={Uri.EscapeDataString(sessionId)}"), null, timeout.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
        }
    }

    private static StringContent Json<T>(T value) where T : class
    {
        var info = (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)LocalSendJsonContext.Default.GetTypeInfo(typeof(T))!;
        return new StringContent(JsonSerializer.Serialize(value, info), System.Text.Encoding.UTF8, "application/json");
    }

    internal static HttpClient CreateHttp(LocalSendPeer peer, TimeSpan timeout)
    {
        var expected = peer.Info.Fingerprint;
        var handler = new SocketsHttpHandler
        {
            UseProxy = false, // máy trong LAN: không đi qua proxy hệ thống
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            SslOptions = new SslClientAuthenticationOptions
            {
                // Chứng thư tự ký của LocalSend: tin đúng chứng thư có vân tay đã công bố, không gì khác.
                RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                    certificate is not null && FingerprintMatches(certificate, expected),
            },
        };
        return new HttpClient(handler) { Timeout = timeout };
    }

    internal static bool FingerprintMatches(X509Certificate certificate, string expected)
    {
        var hash = SHA256.HashData(certificate.GetRawCertData());
        return expected.Length == 64 && string.Equals(Convert.ToHexString(hash), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string MimeOf(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".heic" => "image/heic",
        ".mp4" => "video/mp4",
        ".mov" => "video/quicktime",
        ".pdf" => "application/pdf",
        ".txt" => "text/plain",
        ".zip" => "application/zip",
        _ => "application/octet-stream",
    };

    /// <summary>Thân tệp có Content-Length (máy nhận bắt buộc) và báo tiến độ.</summary>
    private sealed class ProgressContent(Stream source, Action<long> report) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            await SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            var buffer = new byte[1 << 16];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                total += read;
                report(total);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = source.Length;
            return true;
        }
    }
}
