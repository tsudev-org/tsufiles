// ADR-0004 S2: trang "Chia sẻ với điện thoại" và phiên chia sẻ của app desktop.

using System.Net;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using TsuFiles.App.Services;
using TsuFiles.App.ViewModels;
using TsuFiles.App.ViewModels.Pages;
using TsuFiles.App.Views;
using TsuFiles.Core.Localization;
using TsuFiles.Core.Platform;
using TsuFiles.Core.Settings;
using TsuFiles.Core.Share;

namespace TsuFiles.App.Tests;

public sealed class PhoneShareTests
{
    private static PhoneShareService NewService(string folder)
    {
        TestServices.EnsureInitialized("vi");
        return new PhoneShareService(AppHost.Get<IPlatformServices>(), AppHost.Get<ILocalizer>(), NullLogger<PhoneShareService>.Instance)
        {
            ReceiveFolder = folder,
        };
    }

    [Theory]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 16_7_10 like Mac OS X) AppleWebKit/605.1.15 Version/16.6 Mobile/15E148 Safari/604.1", "iPhone")]
    [InlineData("Mozilla/5.0 (iPad; CPU OS 12_5 like Mac OS X) AppleWebKit/605.1.15", "iPad")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 7) AppleWebKit/537.36 Chrome/131.0 Mobile Safari/537.36", "Android")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0", "Windows")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 14_5) AppleWebKit/605.1.15", "Mac")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64) Gecko/20100101 Firefox/133.0", "Linux")]
    [InlineData("curl/8.5.0", null)]
    [InlineData(null, null)]
    public void Devices_are_named_from_their_browser(string? userAgent, string? expected) =>
        Assert.Equal(expected, ShareApproval.NameOf(userAgent));

    [AvaloniaFact]
    public async Task The_receive_folder_is_remembered_and_the_default_is_not_written()
    {
        TestServices.EnsureInitialized("vi");
        var directory = Directory.CreateTempSubdirectory("tsufiles-share-store-").FullName;
        try
        {
            var store = new UserDataStore(directory);
            store.Load();
            await using (var share = new PhoneShareService(AppHost.Get<IPlatformServices>(), AppHost.Get<ILocalizer>(),
                             NullLogger<PhoneShareService>.Instance, store))
            {
                Assert.Equal(share.DefaultReceiveFolder, share.ReceiveFolder);
                share.ReceiveFolder = directory;
            }

            var nextStart = new UserDataStore(directory);
            nextStart.Load();
            Assert.Equal(directory, nextStart.Settings.ShareReceiveFolder);
            await using var again = new PhoneShareService(AppHost.Get<IPlatformServices>(), AppHost.Get<ILocalizer>(),
                NullLogger<PhoneShareService>.Instance, nextStart);
            Assert.Equal(directory, again.ReceiveFolder);

            again.ReceiveFolder = again.DefaultReceiveFolder; // back to the default: the key goes away
            Assert.Null(nextStart.Settings.ShareReceiveFolder);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [AvaloniaFact]
    public async Task A_new_device_waits_for_the_user_and_gets_the_page_once_allowed()
    {
        var folder = Directory.CreateTempSubdirectory("tsufiles-share-").FullName;
        await using var share = NewService(folder);
        var asked = 0;
        share.ApprovalRequested += () => asked++;
        share.Start();
        Assert.True(share.IsRunning);

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (iPhone; CPU iPhone OS 16_7 like Mac OS X)");
        var request = http.GetAsync(share.LoopbackLink, TestContext.Current.CancellationToken);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (share.Pending.Count == 0 && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        var approval = Assert.Single(share.Pending);
        Assert.Equal(1, asked);
        Assert.StartsWith("iPhone (127.0.0.1)", approval.Text);
        approval.AllowCommand.Execute(null);

        var response = await request;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(share.Pending);

        await share.StopAsync();
        Assert.False(share.IsRunning);
        Assert.Empty(share.Links);
        Directory.Delete(folder, true);
    }

    [AvaloniaFact]
    public async Task The_page_shows_a_code_for_the_first_link_and_keeps_files_dropped_before_starting()
    {
        var folder = Directory.CreateTempSubdirectory("tsufiles-share-").FullName;
        var file = Path.Combine(folder, "Báo cáo.txt");
        await File.WriteAllTextAsync(file, "x", TestContext.Current.CancellationToken);
        await using var share = NewService(folder);
        using var page = new SharePageViewModel(share, AppHost.Get<IPlatformServices>());

        page.AddFiles([file, file, folder]);
        Assert.Equal(["Báo cáo.txt"], page.SharedFiles);
        Assert.True(page.IsStopped);
        Assert.Null(page.QrGeometry);

        page.StartCommand.Execute(null);
        Assert.True(page.IsRunning);
        if (page.Link is null)
        {
            Assert.True(page.NoNetwork);
        }
        else
        {
            Assert.NotNull(page.QrGeometry);
            Assert.Equal(TsuFiles.Core.Share.QrCode.Encode(page.Link).Size + 8, page.QrSize);
        }

        page.SetReceiveFolder(Path.GetTempPath()); // ignored while sharing
        Assert.Equal(folder, page.ReceiveFolder);
        await page.StopCommand.ExecuteAsync(null);
        Assert.Null(page.QrGeometry);
        Directory.Delete(folder, true);
    }

    [AvaloniaFact]
    public async Task While_sharing_the_page_shows_the_code_and_the_device_asking_to_connect()
    {
        TestServices.EnsureInitialized("vi");
        var share = AppHost.Get<PhoneShareService>();
        share.Start();
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (iPhone; CPU iPhone OS 16_7 like Mac OS X)");
            _ = http.GetAsync(share.LoopbackLink, TestContext.Current.CancellationToken);
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (share.Pending.Count == 0 && DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }
            Assert.Single(share.Pending);

            using var vm = new MainWindowViewModel(AppHost.Services, AppHost.Get<ILocalizer>(), AppHost.Get<UpdateService>(), AppHost.Get<UserDataStore>());
            vm.Navigate("share");
            var window = new MainWindow { DataContext = vm, Width = 1080, Height = 900 };
            window.Show();
            RenderTests.Pump();
            RenderTests.Save(window, "page-share-running-vi.png");
            window.Close();
        }
        finally
        {
            await share.StopAsync();
        }
        Assert.Empty(share.Pending); // stopping refuses whoever was still waiting
    }

    private static async Task PumpUntilAsync(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!done() && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    [AvaloniaFact]
    public async Task Files_in_the_list_go_to_a_nearby_LocalSend_device_after_it_accepts()
    {
        var folder = Directory.CreateTempSubdirectory("tsufiles-share-").FullName;
        var inbox = Directory.CreateTempSubdirectory("tsufiles-share-in-").FullName;
        var file = Path.Combine(folder, "Ảnh chụp.jpg");
        await File.WriteAllTextAsync(file, "jpeg", TestContext.Current.CancellationToken);
        var accept = true;
        await using var other = new ShareServer(new ShareOptions
        {
            ReceiveFolder = inbox,
            Approve = (_, _) => Task.FromResult(accept),
            LocalSend = new LocalSendOptions { Alias = "iPhone của Su", Discover = false },
        });
        other.Start();
        try
        {
            await using var share = NewService(folder);
            using var page = new SharePageViewModel(share, AppHost.Get<IPlatformServices>());
            page.StartCommand.Execute(null);
            Assert.True(page.NoPeers);

            // Máy kia trả lời gói thông báo (giả lập multicast): hiện trong danh sách.
            Assert.True(share.Discovery!.Observe(IPAddress.Loopback, other.LocalSendSelf!));
            await PumpUntilAsync(() => page.Peers.Count > 0);
            var peer = Assert.Single(page.Peers);
            Assert.False(page.NoPeers);
            Assert.False(page.SendToCommand.CanExecute(peer)); // chưa có tệp nào để gửi

            page.AddFiles([file]);
            Assert.True(page.SendToCommand.CanExecute(peer));
            await page.SendToCommand.ExecuteAsync(peer);
            Assert.Equal("jpeg", await File.ReadAllTextAsync(Path.Combine(inbox, "Ảnh chụp.jpg"), TestContext.Current.CancellationToken));
            Assert.Equal("Đã gửi 1/1 tệp tới iPhone của Su (127.0.0.1).", page.SendStatus);

            accept = false;
            await other.DisposeAsync(); // phiên mới của máy kia sẽ hỏi lại từ đầu
            await using var refusing = new ShareServer(new ShareOptions
            {
                ReceiveFolder = inbox,
                Approve = (_, _) => Task.FromResult(false),
                LocalSend = new LocalSendOptions { Alias = "Máy từ chối", Discover = false },
            });
            refusing.Start();
            share.Discovery.Observe(IPAddress.Loopback, refusing.LocalSendSelf!);
            await PumpUntilAsync(() => page.Peers.Any(p => p.Info.Alias == "Máy từ chối"));
            await page.SendToCommand.ExecuteAsync(page.Peers.First(p => p.Info.Alias == "Máy từ chối"));
            Assert.Equal("Máy từ chối (127.0.0.1) đã từ chối.", page.SendStatus);

            await page.StopCommand.ExecuteAsync(null);
            Assert.Empty(page.Peers);
            Assert.Null(page.SendStatus);
        }
        finally
        {
            Directory.Delete(folder, true);
            Directory.Delete(inbox, true);
        }
    }

    [AvaloniaFact]
    public async Task While_sharing_the_page_lists_nearby_LocalSend_devices()
    {
        TestServices.EnsureInitialized("vi");
        var share = AppHost.Get<PhoneShareService>();
        share.Start();
        try
        {
            share.Discovery!.Observe(IPAddress.Parse("192.168.1.23"), new TsuFiles.Core.Share.LocalSend.LocalSendInfo
            {
                Alias = "iPhone 8 của Su",
                DeviceModel = "iPhone",
                DeviceType = "mobile",
                Fingerprint = "ab12",
            });
            await PumpUntilAsync(() => share.Peers.Count > 0);

            using var vm = new MainWindowViewModel(AppHost.Services, AppHost.Get<ILocalizer>(), AppHost.Get<UpdateService>(), AppHost.Get<UserDataStore>());
            vm.Navigate("share");
            var window = new MainWindow { DataContext = vm, Width = 1080, Height = 1100 };
            window.Show();
            RenderTests.Pump();
            RenderTests.Save(window, "page-share-nearby-vi.png");
            window.Close();
        }
        finally
        {
            await share.StopAsync();
        }
        Assert.Empty(share.Peers);
    }
}
