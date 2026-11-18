// "tsufiles share" (ADR-0004 S1): mở phiên gửi/nhận tệp trong mạng nội bộ, in địa chỉ cho điện thoại mở, hỏi trước
// khi cho mỗi thiết bị mới kết nối. Không cần engine nên chạy trước khi tạo CliHost.

using TsuFiles.Core.Share;

namespace TsuFiles.Cli;

internal static class ShareCommand
{
    /// <summary>Tên máy này hiện trên app LocalSend / TsuFiles khác.</summary>
    internal static string Alias => $"TsuFiles ({Environment.MachineName})";

    public static async Task<int> RunAsync(CliOptions options, TextWriter output, TextWriter errors, CancellationToken cancellationToken)
    {
        var shared = new List<string>();
        foreach (var input in options.Inputs)
        {
            var full = Path.GetFullPath(input);
            if (!File.Exists(full))
            {
                errors.WriteLine(CliLanguage.T($"tsufiles: không tìm thấy tệp: {input}", $"tsufiles: file not found: {input}"));
                return Cli.UsageError;
            }
            shared.Add(full);
        }

        var ask = new SemaphoreSlim(1, 1);
        var interactive = !Console.IsInputRedirected;
        await using var server = new ShareServer(new ShareOptions
        {
            ReceiveFolder = Path.GetFullPath(options.Output ?? Environment.CurrentDirectory),
            SharedFiles = shared,
            LocalSend = new LocalSendOptions { Alias = Alias, DeviceType = "headless" },
            Approve = async (device, token) =>
            {
                if (!interactive)
                {
                    errors.WriteLine(CliLanguage.T($"Từ chối {device.Address}: cần chạy trong cửa sổ dòng lệnh để đồng ý.",
                        $"Refused {device.Address}: run in a terminal to give consent."));
                    return false;
                }
                await ask.WaitAsync(token);
                try
                {
                    var who = device.Alias is null ? $"{device.Address}" : $"\"{device.Alias}\" {device.Address}";
                    output.Write(CliLanguage.T($"Thiết bị {who} ({device.UserAgent ?? "?"}) muốn kết nối. Đồng ý? [y/N] ",
                        $"Device {who} ({device.UserAgent ?? "?"}) wants to connect. Allow? [y/N] "));
                    var answer = await Task.Run(Console.ReadLine, token);
                    return answer?.Trim().ToLowerInvariant() is "y" or "yes" or "c" or "co" or "có";
                }
                finally
                {
                    ask.Release();
                }
            },
        });
        var stopped = new TaskCompletionSource();
        server.Stopped += () => stopped.TrySetResult();
        server.Received += r => output.WriteLine(CliLanguage.T($"Đã nhận: {r.Path}", $"Received: {r.Path}"));
        server.Start();

        var links = server.Links();
        if (links.Count == 0)
        {
            errors.WriteLine(CliLanguage.T("tsufiles: máy chưa kết nối mạng nội bộ (Wi-Fi/Ethernet).",
                "tsufiles: this computer is not on a local network (Wi-Fi/Ethernet)."));
            return Cli.SomeFailed;
        }
        output.WriteLine(CliLanguage.T("Mở địa chỉ này trên trình duyệt của điện thoại (cùng Wi-Fi):",
            "Open this address in the phone's browser (same Wi-Fi):"));
        foreach (var link in links)
            output.WriteLine($"  {link}");
        output.WriteLine(CliLanguage.T($"App LocalSend (điện thoại, máy tính) cùng mạng thấy máy này với tên \"{Alias}\".",
            $"LocalSend apps on the same network see this computer as \"{Alias}\"."));
        output.WriteLine(CliLanguage.T("Ctrl+C để dừng.", "Ctrl+C to stop."));

        try
        {
            await stopped.Task.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        return Cli.Success;
    }
}
