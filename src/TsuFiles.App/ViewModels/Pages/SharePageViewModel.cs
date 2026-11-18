// Trang "Chia sẻ với điện thoại" (ADR-0004 S2): bật/tắt phiên, mã QR của đường dẫn, duyệt thiết bị, tệp mời tải về và
// tệp đã nhận. Phiên thuộc PhoneShareService nên vẫn chạy khi người dùng sang trang khác.

using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TsuFiles.App.Services;
using TsuFiles.Core.Platform;
using TsuFiles.Core.Share;
using TsuFiles.Core.Share.LocalSend;

namespace TsuFiles.App.ViewModels.Pages;

public sealed partial class SharePageViewModel : ObservableObject, IDisposable
{
    private readonly PhoneShareService _share;
    private readonly IPlatformServices _platform;

    public SharePageViewModel(PhoneShareService share, IPlatformServices platform)
    {
        _share = share;
        _platform = platform;
        share.PropertyChanged += OnShareChanged;
        share.SharedFiles.CollectionChanged += OnFilesChanged;
        share.Received.CollectionChanged += OnReceivedChanged;
        share.Peers.CollectionChanged += OnPeersChanged;
        Update();
    }

    public bool IsRunning => _share.IsRunning;

    public bool IsStopped => !_share.IsRunning;

    public string ReceiveFolder => _share.ReceiveFolder;

    /// <summary>Đường dẫn đầu tiên (Wi-Fi/Ethernet có cổng mặc định) là đường dẫn trong mã QR.</summary>
    public string? Link => _share.Links.Count > 0 ? _share.Links[0].ToString() : null;

    /// <summary>Các địa chỉ khác của máy (card mạng thứ hai), để gõ tay khi điện thoại ở mạng đó.</summary>
    public IReadOnlyList<string> OtherLinks => _share.Links.Skip(1).Select(l => l.ToString()).ToList();

    public bool HasOtherLinks => _share.Links.Count > 1;

    public bool NoNetwork => _share.IsRunning && _share.Links.Count == 0;

    [ObservableProperty]
    private Geometry? _qrGeometry;

    /// <summary>Cạnh mã QR tính theo ô, cả viền trắng 4 ô hai bên (Viewbox co giãn theo đó).</summary>
    [ObservableProperty]
    private double _qrSize;

    public ObservableCollection<ShareApproval> Pending => _share.Pending;

    public IReadOnlyList<string> SharedFiles => _share.SharedFiles.Select(Path.GetFileName).ToList()!;

    public bool HasSharedFiles => _share.SharedFiles.Count > 0;

    public IReadOnlyList<string> Received => _share.Received.Select(r => Path.GetFileName(r.Path)).ToList();

    public bool HasReceived => _share.Received.Count > 0;

    /// <summary>Máy LocalSend tìm thấy (ADR-0004 S3b).</summary>
    public ObservableCollection<LocalSendPeer> Peers => _share.Peers;

    public bool NoPeers => _share.IsRunning && _share.Peers.Count == 0;

    public string? SendStatus => _share.SendStatus;

    /// <summary>Nút Gửi chỉ bật khi có tệp trong danh sách và chưa có lần gửi nào đang chạy.</summary>
    public bool CanSend => _share.SharedFiles.Count > 0 && !_share.IsSending;

    [RelayCommand]
    private Task FindPeersAsync() => _share.FindPeersAsync();

    [RelayCommand(CanExecute = nameof(CanSend))]
    private Task SendToAsync(LocalSendPeer peer) => _share.SendAsync(peer);

    [RelayCommand]
    private void Start()
    {
        _share.Start();
        Update();
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        await _share.StopAsync();
        Update();
    }

    [RelayCommand]
    private void RefreshLinks()
    {
        _share.RefreshLinks();
        Update();
    }

    [RelayCommand]
    private void OpenReceiveFolder()
    {
        Directory.CreateDirectory(_share.ReceiveFolder);
        _platform.RevealInFileManager(_share.Received.Count > 0 ? _share.Received[0].Path : _share.ReceiveFolder);
    }

    public void SetReceiveFolder(string folder)
    {
        if (_share.IsRunning)
            return;
        _share.ReceiveFolder = folder;
    }

    public void AddFiles(IEnumerable<string> paths) => _share.AddFiles(paths);

    public void Dispose()
    {
        _share.PropertyChanged -= OnShareChanged;
        _share.SharedFiles.CollectionChanged -= OnFilesChanged;
        _share.Received.CollectionChanged -= OnReceivedChanged;
        _share.Peers.CollectionChanged -= OnPeersChanged;
    }

    private void OnShareChanged(object? sender, PropertyChangedEventArgs e)
    {
        Update();
        OnPropertyChanged(nameof(SendStatus));
        SendToCommand.NotifyCanExecuteChanged();
    }

    private void OnPeersChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(NoPeers));

    private void OnFilesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SharedFiles));
        OnPropertyChanged(nameof(HasSharedFiles));
        SendToCommand.NotifyCanExecuteChanged();
    }

    private void OnReceivedChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Received));
        OnPropertyChanged(nameof(HasReceived));
    }

    private void Update()
    {
        if (Link is { } link)
        {
            var qr = QrCode.Encode(link);
            QrGeometry = Geometry.Parse(qr.ToPathData(4));
            QrSize = qr.Size + 8;
        }
        else
        {
            QrGeometry = null;
            QrSize = 0;
        }
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsStopped));
        OnPropertyChanged(nameof(ReceiveFolder));
        OnPropertyChanged(nameof(Link));
        OnPropertyChanged(nameof(OtherLinks));
        OnPropertyChanged(nameof(HasOtherLinks));
        OnPropertyChanged(nameof(NoNetwork));
        OnPropertyChanged(nameof(NoPeers));
    }
}
