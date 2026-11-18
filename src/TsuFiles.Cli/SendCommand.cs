// "tsufiles send" (ADR-0004 S3): tìm máy trong mạng nội bộ theo giao thức LocalSend (app LocalSend trên điện thoại hay
// máy tính, TsuFiles khác đang bật chia sẻ) và gửi tệp tới một máy. Không có tệp/--to: chỉ liệt kê máy tìm thấy.

using System.Net;
using System.Text.Json;
using TsuFiles.Core.Share;
using TsuFiles.Core.Share.LocalSend;

namespace TsuFiles.Cli;

internal static class SendCommand
{
    public static async Task<int> RunAsync(CliOptions options, TextWriter output, TextWriter errors, CancellationToken cancellationToken)
    {
        var files = new List<string>();
        foreach (var input in options.Inputs)
        {
            var full = Path.GetFullPath(input);
            if (!File.Exists(full))
            {
                errors.WriteLine(CliLanguage.T($"tsufiles: không tìm thấy tệp: {input}", $"tsufiles: file not found: {input}"));
                return Cli.UsageError;
            }
            files.Add(full);
        }

        // Máy chủ nhỏ chỉ để máy khác trả lời /register (cách LocalSend báo "tôi ở đây"); từ chối mọi tệp gửi tới.
        await using var server = new ShareServer(new ShareOptions
        {
            ReceiveFolder = Path.GetTempPath(),
            Approve = (_, _) => Task.FromResult(false),
            LocalSend = new LocalSendOptions { Alias = ShareCommand.Alias, DeviceType = "headless" },
        });
        server.Start();
        var discovery = server.Discovery!;
        var wait = TimeSpan.FromSeconds(options.SettleSeconds ?? 3);
        await discovery.AnnounceAsync(cancellationToken);
        await Task.Delay(wait / 2, cancellationToken);
        await discovery.AnnounceAsync(cancellationToken); // gói UDP có thể rơi: thông báo lần hai
        await Task.Delay(wait / 2, cancellationToken);
        var peers = discovery.Peers;

        if (options.To is null)
        {
            if (options.Json)
            {
                using var stream = new MemoryStream();
                using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    json.WriteStartArray();
                    foreach (var p in peers)
                    {
                        json.WriteStartObject();
                        json.WriteString("alias", p.Info.Alias);
                        json.WriteString("address", p.Address.ToString());
                        json.WriteNumber("port", p.Port);
                        json.WriteString("deviceType", p.Info.DeviceType);
                        json.WriteString("deviceModel", p.Info.DeviceModel);
                        json.WriteEndObject();
                    }
                    json.WriteEndArray();
                }
                output.WriteLine(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
                return Cli.Success;
            }
            if (peers.Count == 0)
            {
                errors.WriteLine(CliLanguage.T("Không thấy máy nào. Mở app LocalSend hoặc bật chia sẻ trong TsuFiles trên máy kia (cùng Wi-Fi).",
                    "No devices found. Open LocalSend, or turn on sharing in TsuFiles, on the other device (same Wi-Fi)."));
                return files.Count > 0 ? Cli.UsageError : Cli.Success;
            }
            foreach (var p in peers)
                output.WriteLine($"  {p.Label}  {p.Info.DeviceType ?? "?"}  {p.Info.DeviceModel ?? ""}".TrimEnd());
            if (files.Count > 0)
                errors.WriteLine(CliLanguage.T("Chọn máy nhận bằng --to <tên hoặc địa chỉ IP>.", "Pick the receiver with --to <name or IP address>."));
            return files.Count > 0 ? Cli.UsageError : Cli.Success;
        }

        var matches = Match(peers, options.To);
        if (matches.Count != 1)
        {
            errors.WriteLine(matches.Count == 0
                ? CliLanguage.T($"tsufiles: không thấy máy '{options.To}'.", $"tsufiles: no device '{options.To}' found.")
                : CliLanguage.T($"tsufiles: nhiều máy khớp '{options.To}', ghi rõ hơn hoặc dùng địa chỉ IP:",
                    $"tsufiles: several devices match '{options.To}', be more specific or use the IP address:"));
            // Hai máy cùng địa chỉ (hai TsuFiles trên một máy): in kèm cổng để chọn bằng --to <IP>:<cổng>.
            foreach (var p in matches.Count == 0 ? peers : matches)
                errors.WriteLine($"  {p.Label}  --to {p.Address}:{p.Port}");
            return Cli.UsageError;
        }
        if (files.Count == 0)
        {
            errors.WriteLine(CliLanguage.T("Hãy chỉ định tệp cần gửi.", "Give the files to send."));
            return Cli.UsageError;
        }

        var peer = matches[0];
        output.WriteLine(CliLanguage.T($"Đang chờ {peer.Label} đồng ý nhận {files.Count} tệp...",
            $"Waiting for {peer.Label} to accept {files.Count} file(s)..."));
        var result = await LocalSendClient.SendAsync(server.LocalSendSelf!, peer, files, cancellationToken: cancellationToken);
        foreach (var sent in result.SentFiles)
            output.WriteLine(CliLanguage.T($"Đã gửi: {Path.GetFileName(sent)}", $"Sent: {Path.GetFileName(sent)}"));
        switch (result.Status)
        {
            case LocalSendSendStatus.Sent when result.SentFiles.Count == files.Count:
                return Cli.Success;
            case LocalSendSendStatus.Sent:
                errors.WriteLine(CliLanguage.T("Máy nhận chỉ nhận một phần (bỏ tệp quá lớn hoặc loại bị chặn).",
                    "The receiver accepted only some files (too large or blocked types)."));
                return Cli.SomeFailed;
            case LocalSendSendStatus.NothingToSend:
                output.WriteLine(CliLanguage.T("Máy nhận báo không cần gửi gì.", "The receiver needs nothing."));
                return Cli.Success;
            default:
                errors.WriteLine(result.Status switch
                {
                    LocalSendSendStatus.Rejected => CliLanguage.T("Máy nhận đã từ chối.", "The receiver declined."),
                    LocalSendSendStatus.PinRequired => CliLanguage.T("Máy nhận đặt mã PIN; tsufiles send chưa hỗ trợ PIN.",
                        "The receiver requires a PIN; tsufiles send does not support PINs yet."),
                    LocalSendSendStatus.Busy => CliLanguage.T("Máy nhận đang bận nhận từ máy khác.", "The receiver is busy with another device."),
                    _ => CliLanguage.T($"Gửi thất bại: {result.Error}", $"Sending failed: {result.Error}"),
                });
                return Cli.SomeFailed;
        }
    }

    internal static List<LocalSendPeer> Match(IReadOnlyList<LocalSendPeer> peers, string to)
    {
        to = to.Trim();
        if (to.Length == 0)
            return []; // --to "" không được khớp mọi máy (Contains("") luôn đúng)
        if (IPAddress.TryParse(to, out var ip))
            return peers.Where(p => p.Address.Equals(ip)).ToList();
        if (IPEndPoint.TryParse(to, out var endpoint) && endpoint.Port > 0)
            return peers.Where(p => p.Address.Equals(endpoint.Address) && p.Port == endpoint.Port).ToList();
        var exact = peers.Where(p => string.Equals(p.Info.Alias, to, StringComparison.OrdinalIgnoreCase)).ToList();
        return exact.Count > 0 ? exact : peers.Where(p => p.Info.Alias.Contains(to, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
