// TsuFiles: script cài/gỡ Linux và macOS nói theo ngôn ngữ hệ thống (LANG), TSUFILES_LANG=vi|en thắng (giống dòng lệnh).

using System.Diagnostics;

namespace TsuFiles.Platform.Tests;

public sealed class ForkLinuxInstallerTests
{
    // PRODUCT_POLICY mục 1.4: hệ thống vi → vi, en → en, ngôn ngữ chưa hỗ trợ (ja) → en; TSUFILES_LANG thắng.
    [Theory]
    [InlineData(null, "vi_VN.UTF-8", "Tùy chọn không hợp lệ: --la", "Cách dùng:")]
    [InlineData(null, "en_US.UTF-8", "Unknown option: --la", "Usage:")]
    [InlineData(null, "ja_JP.UTF-8", "Unknown option: --la", "Usage:")]
    [InlineData(null, null, "Unknown option: --la", "Usage:")]
    [InlineData("vi", "en_US.UTF-8", "Tùy chọn không hợp lệ: --la", "Cách dùng:")]
    [InlineData("EN", "vi_VN.UTF-8", "Unknown option: --la", "Usage:")]
    [InlineData("khong-ro", "vi_VN.UTF-8", "Tùy chọn không hợp lệ: --la", "Cách dùng:")]
    public void Install_script_speaks_the_system_language(string? language, string? lang, string error, string usage)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(), "Cần /bin/sh.");
        var (code, output) = Run("install.sh", language, lang, "--la");
        Assert.Equal(1, code);
        Assert.Contains(error, output);
        Assert.Contains(usage, output);
    }

    [Fact]
    public void Every_message_of_the_scripts_has_both_languages()
    {
        foreach (var path in new[] { "linux/install.sh", "linux/uninstall.sh", "macos/install.sh", "macos/uninstall.sh" })
        {
            var lines = File.ReadAllLines(Path.Combine(InstallerFolder, "..", path));
            // Thông báo cho người dùng đi qua say "<vi>" "<en>"; printf còn lại chỉ ghi tệp đánh dấu hoặc lệnh mv để chép.
            var plain = lines.Where(l => l.TrimStart().StartsWith("printf '%s\\n' \"", StringComparison.Ordinal)
                                         && !l.Contains("upgrade_token", StringComparison.Ordinal)
                                         && !l.Contains("\"$1\"", StringComparison.Ordinal)
                                         && !l.Contains("  mv '", StringComparison.Ordinal));
            Assert.Empty(plain);
            Assert.Contains(lines, l => l.TrimStart().StartsWith("say ", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("linux")]
    [InlineData("macos")]
    public void Packages_carry_the_vietnamese_install_guide(string platform)
    {
        var guide = Path.Combine(InstallerFolder, "..", platform, "HUONG-DAN-CAI-DAT.txt");
        Assert.True(File.Exists(guide), guide);
        Assert.Contains("tsudev-org/tsufiles/releases", File.ReadAllText(guide));
        var build = File.ReadAllText(Path.Combine(InstallerFolder, "..", "..", "build", "build-portable.ps1"));
        Assert.Contains($"'installer/{platform}/HUONG-DAN-CAI-DAT.txt'", build);
    }

    private static string InstallerFolder
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TsuFiles.slnx")))
                dir = dir.Parent;
            return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("TsuFiles.slnx"), "installer", "linux");
        }
    }

    private static (int Code, string Output) Run(string script, string? language, string? lang, params string[] args)
    {
        var psi = new ProcessStartInfo("/bin/sh")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(Path.Combine(InstallerFolder, script));
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
        foreach (var name in new[] { "TSUFILES_LANG", "LC_ALL", "LC_MESSAGES", "LANG" })
            psi.Environment.Remove(name);
        if (language is not null)
            psi.Environment["TSUFILES_LANG"] = language;
        if (lang is not null)
            psi.Environment["LANG"] = lang;
        using var process = Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit(10000);
        return (process.ExitCode, output);
    }
}
