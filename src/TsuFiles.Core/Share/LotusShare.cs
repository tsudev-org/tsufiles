// TsuFiles: "Gửi bằng Lotus Share" - đưa tệp kết quả sang ứng dụng Lotus Share đã cài (ADR-0004 mục 4, phiếu
// logs/handover/20261119-TC1_tsufiles-bo-localsend-va-ten-cu.md việc B; hợp đồng: lotus-share docs/MO-KEM-TEP.md). TsuFiles KHÔNG nói giao thức mạng của Lotus
// Share: chỉ mở ứng dụng đó kèm đường dẫn tệp, như người dùng kéo tệp vào cửa sổ của nó. Lotus Share bản máy tính nhận
// đường dẫn tệp/thư mục ở dòng lệnh; đang chạy sẵn thì tiến trình mới chuyển danh sách sang cửa sổ đang mở rồi thoát.

using System.Diagnostics;
using Microsoft.Win32;

namespace TsuFiles.Core.Share;

public static class LotusShare
{
    /// <summary>Trang tải Lotus Share.</summary>
    public const string DownloadUrl = "https://lotus.tsudev.com";

    /// <summary>AppId của bộ cài Windows (Inno Setup) - khóa gỡ cài đặt là "{AppId}_is1".</summary>
    internal const string WindowsUninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{21D09887-EE8E-478B-B43F-BD492DE6EFE3}_is1";

    internal const string WindowsExe = "lotus_share.exe";
    internal const string MacBundle = "Lotus Share.app";
    internal const string MacBundleId = "com.tsudev.lotusshare";

    /// <summary>
    /// Những chỗ Lotus Share có thể nằm, theo thứ tự ưu tiên. Chỉ là đường dẫn cố định của bộ cài chính thức (không
    /// dò PATH): TsuFiles không chạy một chương trình cùng tên đặt ở chỗ khác.
    /// </summary>
    /// <param name="os">"windows", "macos" hoặc "linux".</param>
    /// <param name="env">Đọc biến môi trường (ProgramFiles, LOCALAPPDATA, HOME).</param>
    /// <param name="windowsInstallDirs">Thư mục cài ghi trong registry (InstallLocation), nếu có.</param>
    public static IReadOnlyList<string> Candidates(string os, Func<string, string?> env, IEnumerable<string> windowsInstallDirs)
    {
        var list = new List<string>();
        switch (os)
        {
            case "windows":
                foreach (var dir in windowsInstallDirs)
                    list.Add(Path.Combine(dir, WindowsExe));
                if (env("ProgramFiles") is { Length: > 0 } programFiles)
                    list.Add(Path.Combine(programFiles, "Lotus Share", WindowsExe));
                if (env("LOCALAPPDATA") is { Length: > 0 } local)
                    list.Add(Path.Combine(local, "Programs", "Lotus Share", WindowsExe));
                break;
            case "macos":
                list.Add(Path.Combine("/Applications", MacBundle));
                if (env("HOME") is { Length: > 0 } home)
                    list.Add(Path.Combine(home, "Applications", MacBundle));
                break;
            case "linux":
                // Gói APT/RPM: /opt/lotus-share/lotus_share (+ liên kết /usr/bin/lotus-share).
                list.Add("/opt/lotus-share/lotus_share");
                list.Add("/usr/bin/lotus-share");
                break;
        }
        return list;
    }

    /// <summary>Đường dẫn Lotus Share đã cài trên máy này (tệp chạy, hoặc gói .app trên macOS), hoặc null.</summary>
    public static string? Find()
    {
        var os = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : OperatingSystem.IsLinux() ? "linux" : "";
        var candidates = Candidates(os, Environment.GetEnvironmentVariable, OperatingSystem.IsWindows() ? WindowsInstallDirs() : []);
        return candidates.FirstOrDefault(p => os == "macos" ? Directory.Exists(p) : File.Exists(p));
    }

    /// <summary>
    /// Lệnh mở Lotus Share kèm <paramref name="paths"/>. Đường dẫn đi qua ArgumentList (không qua shell) và luôn là
    /// đường dẫn tuyệt đối, nên không bao giờ bị Lotus Share hiểu nhầm thành cờ dòng lệnh (cờ bắt đầu bằng "-").
    /// </summary>
    public static ProcessStartInfo StartInfo(string app, IEnumerable<string> paths)
    {
        var mac = app.EndsWith(".app", StringComparison.OrdinalIgnoreCase);
        // macOS: open(1) giao tệp cho ứng dụng qua sự kiện "mở tệp" (AppDelegate openFiles), không qua argv; gọi theo
        // mã gói (-b) - bền hơn đường dẫn .app (lotus-share docs/MO-KEM-TEP.md).
        var start = new ProcessStartInfo(mac ? "/usr/bin/open" : app) { UseShellExecute = false };
        if (mac)
        {
            start.ArgumentList.Add("-b");
            start.ArgumentList.Add(MacBundleId);
        }
        foreach (var path in paths)
            start.ArgumentList.Add(Path.GetFullPath(path));
        return start;
    }

    /// <summary>Mở Lotus Share ở <paramref name="app"/> kèm các tệp/thư mục còn trên đĩa. Trả về false nếu không còn gì để gửi.</summary>
    public static bool Send(string app, IEnumerable<string> paths)
    {
        var existing = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (existing.Count == 0)
            return false;
        Process.Start(StartInfo(app, existing))?.Dispose();
        return true;
    }

    private static IEnumerable<string> WindowsInstallDirs()
    {
        if (!OperatingSystem.IsWindows())
            yield break;
        foreach (var (hive, view) in new[]
                 {
                     (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.LocalMachine, RegistryView.Registry32),
                     (RegistryHive.CurrentUser, RegistryView.Default),
                 })
        {
            string? dir = null;
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var key = root.OpenSubKey(WindowsUninstallKey);
                dir = key?.GetValue("InstallLocation") as string;
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                // Không đọc được nhánh này: thử nhánh sau.
            }
            if (!string.IsNullOrWhiteSpace(dir))
                yield return dir;
        }
    }
}
