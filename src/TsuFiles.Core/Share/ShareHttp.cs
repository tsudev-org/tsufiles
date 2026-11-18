// HTTP/1.1 tối giản cho ShareServer (ADR-0004 S1): đọc đầu yêu cầu có giới hạn, viết phản hồi kèm tiêu đề bảo mật.
// Mỗi kết nối một yêu cầu (Connection: close) nên không có keep-alive hay pipelining phải xử lý.

using System.Globalization;
using System.Text;

namespace TsuFiles.Core.Share;

public sealed class ShareRequest
{
    private readonly Dictionary<string, string> _headers;
    private readonly Dictionary<string, string> _query;

    internal ShareRequest(string method, string path, Dictionary<string, string> query, Dictionary<string, string> headers)
    {
        Method = method;
        Path = path;
        _query = query;
        _headers = headers;
    }

    public string Method { get; }

    /// <summary>Đường dẫn đã giải mã %xx, không có phần query.</summary>
    public string Path { get; }

    public string? Header(string name) => _headers.GetValueOrDefault(name.ToLowerInvariant());

    public string? Query(string name) => _query.GetValueOrDefault(name);

    /// <summary>Content-Length hợp lệ (số không âm, chỉ một giá trị), hoặc null.</summary>
    public long? ContentLength =>
        Header("content-length") is { } value
        && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var length) ? length : null;
}

public static class ShareHttp
{
    private const int MaxHeaders = 64;

    /// <summary>
    /// Đọc dòng yêu cầu và tiêu đề (tối đa <see cref="ShareServer.MaxHeadBytes"/>). Trả về yêu cầu (null nếu sai dạng)
    /// và phần byte đã đọc quá đầu yêu cầu (đầu thân).
    /// </summary>
    public static async Task<(ShareRequest? Request, ReadOnlyMemory<byte> Leftover)> ReadRequestAsync(Stream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[ShareServer.MaxHeadBytes];
        var filled = 0;
        while (filled < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(filled), cancellationToken);
            if (read == 0)
                return (null, default);
            filled += read;
            var end = buffer.AsSpan(0, filled).IndexOf("\r\n\r\n"u8);
            if (end >= 0)
                return (Parse(Encoding.Latin1.GetString(buffer, 0, end)), buffer.AsMemory(end + 4, filled - end - 4));
        }
        return (null, default);
    }

    internal static ShareRequest? Parse(string head)
    {
        var lines = head.Split("\r\n");
        var parts = lines[0].Split(' ');
        if (parts.Length != 3 || parts[0] is not ("GET" or "POST") || parts[2] is not ("HTTP/1.1" or "HTTP/1.0")
            || !parts[1].StartsWith('/') || lines.Length - 1 > MaxHeaders)
            return null;

        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0 || line[..colon].Any(c => c is ' ' or '\t'))
                return null; // không có tên, hoặc khoảng trắng trước dấu hai chấm (RFC 9112 mục 5.1)
            var name = line[..colon].ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();
            if (!headers.TryAdd(name, value) && name is "content-length" or "transfer-encoding" or "host")
                return null; // trùng tiêu đề định khung thân: dấu hiệu request smuggling
        }

        var target = parts[1];
        var q = target.IndexOf('?');
        string path;
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            path = Uri.UnescapeDataString(q < 0 ? target : target[..q]);
            if (q >= 0)
                foreach (var pair in target[(q + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var eq = pair.IndexOf('=');
                    var key = Uri.UnescapeDataString((eq < 0 ? pair : pair[..eq]).Replace('+', ' '));
                    var value = eq < 0 ? "" : Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
                    query.TryAdd(key, value);
                }
        }
        catch (UriFormatException)
        {
            return null;
        }
        if (path.Contains("..", StringComparison.Ordinal) || path.Contains('\\') || path.Contains('\0'))
            return null;
        return new ShareRequest(parts[0], path, query, headers);
    }

    public static Task WriteTextAsync(Stream stream, int status, string text, CancellationToken cancellationToken) =>
        WriteAsync(stream, status, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(text), cancellationToken);

    public static async Task WriteAsync(Stream stream, int status, string contentType, byte[] body, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(Head(status, contentType, body.Length, null), cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
    }

    public static Task WriteFileHeadAsync(Stream stream, string fileName, long length, CancellationToken cancellationToken) =>
        stream.WriteAsync(Head(200, "application/octet-stream", length, fileName), cancellationToken).AsTask();

    internal static byte[] Head(int status, string contentType, long length, string? attachment)
    {
        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"HTTP/1.1 {status} {Reason(status)}\r\n")
            .Append(CultureInfo.InvariantCulture, $"Content-Type: {contentType}\r\n")
            .Append(CultureInfo.InvariantCulture, $"Content-Length: {length}\r\n")
            .Append("Connection: close\r\n")
            .Append("Cache-Control: no-store\r\n")
            .Append("X-Content-Type-Options: nosniff\r\n")
            .Append("Referrer-Policy: no-referrer\r\n")
            .Append("X-Frame-Options: DENY\r\n")
            .Append("Content-Security-Policy: default-src 'none'; script-src 'self'; style-src 'unsafe-inline'; connect-src 'self'; ")
            .Append("img-src 'self' data:; base-uri 'none'; form-action 'none'; frame-ancestors 'none'\r\n");
        if (attachment is not null)
        {
            // filename= chỉ ASCII (trình duyệt cũ), filename*= UTF-8 cho tên tiếng Việt (RFC 6266/8187).
            var ascii = new string(attachment.Select(c => c is >= ' ' and < (char)127 and not ('"' or '\\') ? c : '_').ToArray());
            text.Append(CultureInfo.InvariantCulture,
                $"Content-Disposition: attachment; filename=\"{ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(attachment)}\r\n");
        }
        return Encoding.ASCII.GetBytes(text.Append("\r\n").ToString());
    }

    private static string Reason(int status) => status switch
    {
        200 => "OK",
        204 => "No Content",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        411 => "Length Required",
        413 => "Content Too Large",
        415 => "Unsupported Media Type",
        409 => "Conflict",
        422 => "Unprocessable Content",
        429 => "Too Many Requests",
        _ => "Error",
    };
}
