// Phiên "Chia sẻ với điện thoại" của app desktop (ADR-0004 S2). Singleton: trang cài đặt được dựng lại mỗi lần ghé,
// còn phiên chia sẻ phải sống tiếp. Mọi thay đổi trạng thái chạy trên luồng giao diện.

using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TsuFiles.Core.Localization;
using TsuFiles.Core.Platform;
using TsuFiles.Core.Settings;
using TsuFiles.Core.Share;
using TsuFiles.Core.Share.LocalSend;

namespace TsuFiles.App.Services;

public sealed partial class PhoneShareService : ObservableObject, IAsyncDisposable
{
    private readonly ILogger<PhoneShareService> _log;
    private readonly ILocalizer _loc;
    private readonly UserDataStore? _store;
    private ShareServer? _server;
    private CancellationTokenSource? _sending;

    public PhoneShareService(IPlatformServices platform, ILocalizer loc, ILogger<PhoneShareService> log, UserDataStore? store = null)
    {
        _log = log;
        _loc = loc;
        _store = store;
        DefaultReceiveFolder = Path.Combine(platform.DownloadsFolder, "TsuFiles");
        _receiveFolder = string.IsNullOrWhiteSpace(store?.Settings.ShareReceiveFolder) ? DefaultReceiveFolder : store.Settings.ShareReceiveFolder;
    }

    public string DefaultReceiveFolder { get; }

    /// <summary>Thư mục lưu tệp điện thoại gửi lên; đổi được khi phiên chưa chạy, nhớ qua các lần mở app.</summary>
    [ObservableProperty]
    private string _receiveFolder;

    /// <summary>Một thiết bị mới đang chờ duyệt: app mở cửa sổ chính ở trang chia sẻ (kể cả khi đang ẩn ở khay).</summary>
    public event Action? ApprovalRequested;

    partial void OnReceiveFolderChanged(string value)
    {
        if (_store is null)
            return;
        var saved = string.Equals(value, DefaultReceiveFolder, StringComparison.OrdinalIgnoreCase) ? null : value;
        if (_store.Settings.ShareReceiveFolder == saved)
            return;
        _store.Settings.ShareReceiveFolder = saved;
        _store.SaveSettings();
    }

    [ObservableProperty]
    private IReadOnlyList<Uri> _links = [];

    public bool IsRunning => _server is not null;

    /// <summary>Đường dẫn qua 127.0.0.1 (chỉ cho test: máy CI có thể không có địa chỉ mạng nội bộ).</summary>
    internal Uri? LoopbackLink => _server is { } server ? new Uri($"http://127.0.0.1:{server.Port}/s/{server.Token}/") : null;

    /// <summary>Tệp mời điện thoại tải về (giữ cả khi chưa bật, để kéo thả trước rồi mới bật).</summary>
    public ObservableCollection<string> SharedFiles { get; } = [];

    /// <summary>Tệp đã nhận, mới nhất trên cùng.</summary>
    public ObservableCollection<ShareReceived> Received { get; } = [];

    /// <summary>Thiết bị đang chờ người dùng cho phép.</summary>
    public ObservableCollection<ShareApproval> Pending { get; } = [];

    /// <summary>Máy LocalSend tìm thấy khi đang chia sẻ (app LocalSend, TsuFiles khác), mới thấy nhất trước (ADR-0004 S3b).</summary>
    public ObservableCollection<LocalSendPeer> Peers { get; } = [];

    /// <summary>Đang gửi tệp tới một máy LocalSend (mỗi lần một máy).</summary>
    [ObservableProperty]
    private bool _isSending;

    /// <summary>Kết quả lần gửi gần nhất, hiện dưới danh sách máy.</summary>
    [ObservableProperty]
    private string? _sendStatus;

    /// <summary>Danh bạ LocalSend của phiên đang chạy (test dùng để thêm máy không cần multicast).</summary>
    internal LocalSendDiscovery? Discovery => _server?.Discovery;

    /// <summary>Bật phiên mới (mã phiên mới, nên mã QR cũ hết hiệu lực).</summary>
    public void Start()
    {
        if (_server is not null)
            return;
        var server = new ShareServer(new ShareOptions
        {
            ReceiveFolder = ReceiveFolder,
            SharedFiles = [.. SharedFiles],
            Approve = AskAsync,
            // ADR-0004 S3: app LocalSend (iOS 15+, Android, máy tính) và TsuFiles khác thấy máy này và gửi tệp tới được.
            LocalSend = new LocalSendOptions { Alias = $"TsuFiles ({Environment.MachineName})" },
        }, _log);
        server.Received += r => Dispatcher.UIThread.Post(() => Received.Insert(0, r));
        server.Stopped += () => Dispatcher.UIThread.Post(() => Forget(server));
        server.Start();
        _server = server;
        if (server.Discovery is { } discovery)
            discovery.PeersChanged += () => Dispatcher.UIThread.Post(() => SyncPeers(server));
        Links = server.Links();
        OnPropertyChanged(nameof(IsRunning));
    }

    public async Task StopAsync()
    {
        if (_server is not { } server)
            return;
        Forget(server);
        await server.DisposeAsync();
    }

    /// <summary>Thêm tệp (bỏ thư mục, tệp trùng); phiên đang chạy thì điện thoại thấy khi tải lại trang.</summary>
    public void AddFiles(IEnumerable<string> paths)
    {
        var added = paths.Where(File.Exists).Select(Path.GetFullPath)
            .Where(p => !SharedFiles.Contains(p, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var path in added)
            SharedFiles.Add(path);
        _server?.AddFiles(added);
    }

    /// <summary>Đọc lại địa chỉ mạng (máy vừa đổi Wi-Fi).</summary>
    public void RefreshLinks()
    {
        if (_server is { } server)
            Links = server.Links();
    }

    /// <summary>Gửi lại gói thông báo để máy LocalSend xung quanh trả lời (nút làm mới cạnh danh sách máy).</summary>
    public async Task FindPeersAsync()
    {
        if (_server?.Discovery is { } discovery)
            await discovery.AnnounceAsync();
    }

    /// <summary>
    /// Gửi các tệp trong <see cref="SharedFiles"/> tới <paramref name="peer"/>; máy kia phải đồng ý. Kết quả ghi vào
    /// <see cref="SendStatus"/>. Tắt chia sẻ thì hủy lần gửi đang chạy.
    /// </summary>
    public async Task SendAsync(LocalSendPeer peer)
    {
        if (_server?.LocalSendSelf is not { } self || IsSending)
            return;
        var files = SharedFiles.Where(File.Exists).ToList();
        if (files.Count == 0)
            return;
        using var sending = new CancellationTokenSource();
        _sending = sending;
        IsSending = true;
        SendStatus = _loc.Format("share.sending", peer.Label);
        try
        {
            var result = await LocalSendClient.SendAsync(self, peer, files, cancellationToken: sending.Token);
            SendStatus = result.Status switch
            {
                LocalSendSendStatus.Sent or LocalSendSendStatus.NothingToSend =>
                    _loc.Format("share.sent", result.SentFiles.Count, files.Count, peer.Label),
                LocalSendSendStatus.Rejected => _loc.Format("share.send_declined", peer.Label),
                LocalSendSendStatus.Busy => _loc.Format("share.send_busy", peer.Label),
                LocalSendSendStatus.PinRequired => _loc.Format("share.send_pin", peer.Label),
                _ => _loc.Format("share.send_failed", peer.Label, result.Error ?? "?"),
            };
            _log.LogInformation("Share (LocalSend): sent {Sent}/{Total} files to {Peer}: {Status}", result.SentFiles.Count, files.Count,
                peer.Label, result.Status);
        }
        catch (OperationCanceledException)
        {
            SendStatus = null;
        }
        finally
        {
            _sending = null;
            IsSending = false;
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private void SyncPeers(ShareServer server)
    {
        if (!ReferenceEquals(_server, server) || server.Discovery is not { } discovery)
            return;
        Peers.Clear();
        foreach (var peer in discovery.Peers)
            Peers.Add(peer);
    }

    private void Forget(ShareServer server)
    {
        if (!ReferenceEquals(_server, server))
            return;
        _server = null;
        Links = [];
        Peers.Clear();
        SendStatus = null;
        try
        {
            _sending?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        foreach (var pending in Pending.ToList())
            pending.Answer(false);
        OnPropertyChanged(nameof(IsRunning));
    }

    private async Task<bool> AskAsync(ShareDevice device, CancellationToken cancellationToken)
    {
        // Tên LocalSend do thiết bị tự khai: luôn kèm địa chỉ IP trong câu hỏi (share.approve) nên không giả mạo được chỗ.
        var name = device.Alias ?? ShareApproval.NameOf(device.UserAgent) ?? _loc["share.device_unknown"];
        var approval = new ShareApproval(device, this, _loc.Format("share.approve", name, device.Address));
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Pending.Add(approval);
            ApprovalRequested?.Invoke();
        });
        await using (cancellationToken.Register(() => approval.Answer(false)))
            return await approval.Result;
    }

    internal void Remove(ShareApproval approval)
    {
        if (Dispatcher.UIThread.CheckAccess())
            Pending.Remove(approval);
        else
            Dispatcher.UIThread.Post(() => Pending.Remove(approval));
    }
}

/// <summary>Một thiết bị xin kết nối: người dùng bấm Cho phép hay Từ chối (hết giờ thì coi như từ chối).</summary>
public sealed partial class ShareApproval(ShareDevice device, PhoneShareService owner, string text)
{
    private readonly TaskCompletionSource<bool> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ShareDevice Device { get; } = device;

    /// <summary>"iPhone (192.168.1.5) muốn kết nối. Cho phép?"</summary>
    public string Text { get; } = text;

    public Task<bool> Result => _result.Task;

    [RelayCommand]
    private void Allow() => Answer(true);

    [RelayCommand]
    private void Deny() => Answer(false);

    internal void Answer(bool allowed)
    {
        if (_result.TrySetResult(allowed))
            owner.Remove(this);
    }

    /// <summary>Tên dễ đọc từ User-Agent: iPhone, iPad, Android, Windows, Mac, Linux; không rõ thì null.</summary>
    internal static string? NameOf(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return null;
        // Thứ tự quan trọng: User-Agent của iPhone/Android cũng chứa "Mac OS X"/"Linux".
        foreach (var (marker, name) in new[]
                 {
                     ("iPhone", "iPhone"), ("iPad", "iPad"), ("Android", "Android"), ("Windows", "Windows"),
                     ("Macintosh", "Mac"), ("CrOS", "ChromeOS"), ("Linux", "Linux"),
                 })
        {
            if (userAgent.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return name;
        }
        return null;
    }
}
