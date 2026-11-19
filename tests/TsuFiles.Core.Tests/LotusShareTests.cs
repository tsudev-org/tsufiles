// TsuFiles: "Gửi bằng Lotus Share" chỉ mở ứng dụng Lotus Share đã cài kèm đường dẫn tệp (không nói giao thức mạng).

using TsuFiles.Core.Share;

namespace TsuFiles.Core.Tests;

public class LotusShareTests
{
    private static string? Env(string name) => name switch
    {
        "ProgramFiles" => @"C:\Program Files",
        "LOCALAPPDATA" => @"C:\Users\a\AppData\Local",
        "HOME" => "/Users/a",
        _ => null,
    };

    [Fact]
    public void Windows_prefers_the_install_location_the_installer_wrote()
    {
        var list = LotusShare.Candidates("windows", Env, [@"D:\Apps\Lotus Share"]);
        Assert.Equal(Path.Combine(@"D:\Apps\Lotus Share", "lotus_share.exe"), list[0]);
        Assert.Contains(Path.Combine(@"C:\Program Files", "Lotus Share", "lotus_share.exe"), list);
        Assert.Contains(Path.Combine(@"C:\Users\a\AppData\Local", "Programs", "Lotus Share", "lotus_share.exe"), list);
    }

    [Fact]
    public void Linux_and_macOS_look_only_where_the_official_packages_install()
    {
        Assert.Equal(["/opt/lotus-share/lotus_share", "/usr/bin/lotus-share"], LotusShare.Candidates("linux", Env, []));
        Assert.Equal(["/Applications/Lotus Share.app", "/Users/a/Applications/Lotus Share.app"], LotusShare.Candidates("macos", Env, []));
        Assert.Empty(LotusShare.Candidates("freebsd", Env, []));
    }

    [Fact]
    public void Paths_go_as_separate_absolute_arguments_never_through_a_shell()
    {
        var start = LotusShare.StartInfo("/opt/lotus-share/lotus_share", ["a b.jpg", "-t"]);
        Assert.False(start.UseShellExecute);
        Assert.Equal("/opt/lotus-share/lotus_share", start.FileName);
        Assert.Equal(2, start.ArgumentList.Count);
        // A file named like a flag ("-t" = send text) reaches Lotus Share as a full path, so it stays a file.
        Assert.All(start.ArgumentList, a => Assert.True(Path.IsPathRooted(a)));
        Assert.EndsWith("a b.jpg", start.ArgumentList[0]);
    }

    [Fact]
    public void On_macOS_the_files_are_handed_over_through_open()
    {
        var start = LotusShare.StartInfo("/Applications/Lotus Share.app", ["/tmp/x.pdf"]);
        Assert.Equal("/usr/bin/open", start.FileName);
        Assert.Equal(["-b", "com.tsudev.lotusshare", Path.GetFullPath("/tmp/x.pdf")], start.ArgumentList);
    }

    [Fact]
    public void Nothing_starts_when_no_result_is_left_on_disk()
    {
        var gone = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "x.jpg");
        Assert.False(LotusShare.Send("/nonexistent/lotus_share", [gone]));
    }
}
