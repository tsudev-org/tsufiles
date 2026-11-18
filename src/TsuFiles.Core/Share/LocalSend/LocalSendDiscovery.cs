// Tìm máy trong mạng nội bộ theo giao thức LocalSend v2 (ADR-0004 S3): gửi gói thông báo UDP multicast
// 224.0.0.167:53317, nghe thông báo của máy khác và trả lời bằng POST /register (dự phòng: gói UDP announce=false).
// Máy tìm được: app LocalSend (Android, iOS 15+ - gồm iPhone 8, Windows, macOS, Linux) và TsuFiles khác.
//
// Bảo mật: bỏ gói từ địa chỉ ngoài mạng nội bộ, gói quá lớn hay sai JSON; danh bạ có trần số máy. Danh bạ chỉ để
// hiển thị và chọn nơi gửi - nhận tệp vẫn phải qua bước người dùng duyệt của ShareServer.

using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TsuFiles.Core.Web;

namespace TsuFiles.Core.Share.LocalSend;

/// <summary>Một máy đã thấy trong mạng.</summary>
public sealed record LocalSendPeer(IPAddress Address, LocalSendInfo Info, DateTimeOffset LastSeen)
{
    public int Port => Info.Port is > 0 and < 65536 ? Info.Port.Value : LocalSendProtocol.DefaultPort;

    public bool Https => !string.Equals(Info.Protocol, "http", StringComparison.OrdinalIgnoreCase);

    public Uri BaseUri => new($"{(Https ? "https" : "http")}://{Address}:{Port}{LocalSendProtocol.ApiPrefix}");

    /// <summary>Tên hiển thị kèm địa chỉ (bí danh do máy kia tự khai, nên luôn hiện cả IP).</summary>
    public string Label => $"{(string.IsNullOrWhiteSpace(Info.Alias) ? "?" : Info.Alias)} ({Address})";
}

public sealed class LocalSendDiscovery : IAsyncDisposable
{
    public const int MaxPeers = 256;
    private const int MaxPacketBytes = 16 * 1024;

    /// <summary>Số lần trả lời /register đang chạy cùng lúc tối đa: lũ gói thông báo không biến thành lũ kết nối ra.</summary>
    internal const int MaxConcurrentRegisterBacks = 4;

    private readonly LocalSendInfo _self;
    private readonly int _port;
    private readonly IPAddress _group;
    private readonly ILogger _log;
    private readonly Func<LocalSendPeer, CancellationToken, Task<bool>>? _registerBack;
    private readonly Dictionary<string, LocalSendPeer> _peers = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private UdpClient? _udp;
    private Task? _loop;
    private int _disposed;
    private int _registering;

    /// <param name="self">Thông tin của máy này (cần Port, Protocol).</param>
    /// <param name="registerBack">Trả lời một thông báo bằng HTTP /register; false thì trả lời bằng gói UDP.</param>
    /// <param name="port">Cổng multicast (test dùng cổng khác để không đụng LocalSend thật trên máy).</param>
    public LocalSendDiscovery(LocalSendInfo self, Func<LocalSendPeer, CancellationToken, Task<bool>>? registerBack = null,
        ILogger? log = null, int port = LocalSendProtocol.DefaultPort, IPAddress? group = null)
    {
        _self = self with { Announce = null };
        _registerBack = registerBack;
        _log = log ?? NullLogger.Instance;
        _port = port;
        _group = group ?? IPAddress.Parse(LocalSendProtocol.MulticastGroup);
    }

    public string Fingerprint => _self.Fingerprint;

    /// <summary>Máy đã thấy, mới nhất trước.</summary>
    public IReadOnlyList<LocalSendPeer> Peers
    {
        get
        {
            lock (_gate)
                return [.. _peers.Values.OrderByDescending(p => p.LastSeen)];
        }
    }

    public event Action? PeersChanged;

    /// <summary>Mở cổng UDP và tham gia nhóm multicast trên mọi card mạng nội bộ. Không mở được thì chỉ ghi log.</summary>
    public bool Start()
    {
        try
        {
            var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
            udp.MulticastLoopback = true;
            var joined = 0;
            foreach (var local in ShareAddresses.LocalIPv4().Append(IPAddress.Loopback))
            {
                try
                {
                    udp.JoinMulticastGroup(_group, local);
                    joined++;
                }
                catch (SocketException ex)
                {
                    _log.LogDebug(ex, "LocalSend: cannot join {Group} on {Address}", _group, local);
                }
            }
            if (joined == 0)
                udp.JoinMulticastGroup(_group);
            _udp = udp;
            _loop = ReceiveLoopAsync(udp, _stop.Token);
            _log.LogInformation("LocalSend discovery listening on UDP {Port}", _port);
            return true;
        }
        catch (SocketException ex)
        {
            _log.LogWarning(ex, "LocalSend discovery unavailable on UDP {Port}", _port);
            return false;
        }
    }

    /// <summary>Gửi gói thông báo để các máy khác trả lời (gọi lúc bật và khi người dùng bấm làm mới).</summary>
    public Task AnnounceAsync(CancellationToken cancellationToken = default) => SendAsync(announce: true, cancellationToken);

    /// <summary>Ghi nhận một máy (từ gói multicast hay từ POST /register). Trả về false nếu bị bỏ qua.</summary>
    public bool Observe(IPAddress address, LocalSendInfo info)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        if (!LocalNetwork.IsLocal(address) || string.IsNullOrEmpty(info.Fingerprint) || info.Fingerprint.Length > 256
            || info.Fingerprint == _self.Fingerprint || info.Alias.Length > 256)
            return false;
        var peer = new LocalSendPeer(address, info with { Announce = null }, DateTimeOffset.UtcNow);
        lock (_gate)
        {
            if (!_peers.ContainsKey(info.Fingerprint) && _peers.Count >= MaxPeers)
            {
                var oldest = _peers.Values.MinBy(p => p.LastSeen)!;
                _peers.Remove(oldest.Info.Fingerprint);
            }
            _peers[info.Fingerprint] = peer;
        }
        PeersChanged?.Invoke();
        return true;
    }

    public void Clear()
    {
        lock (_gate)
            _peers.Clear();
        PeersChanged?.Invoke();
    }

    /// <summary>Xử lý một gói nhận được (tách riêng để test không cần multicast thật).</summary>
    internal async Task HandlePacketAsync(ReadOnlyMemory<byte> data, IPEndPoint from, CancellationToken cancellationToken)
    {
        if (data.Length > MaxPacketBytes)
            return;
        LocalSendInfo? info;
        try
        {
            info = JsonSerializer.Deserialize(data.Span, LocalSendJsonContext.Default.LocalSendInfo);
        }
        catch (JsonException)
        {
            return;
        }
        if (info is null || !Observe(from.Address, info) || info.Announce != true)
            return;

        var peer = new LocalSendPeer(from.Address.IsIPv4MappedToIPv6 ? from.Address.MapToIPv4() : from.Address, info, DateTimeOffset.UtcNow);
        var registered = false;
        if (_registerBack is not null)
        {
            if (Interlocked.Increment(ref _registering) > MaxConcurrentRegisterBacks)
            {
                Interlocked.Decrement(ref _registering);
                _log.LogDebug("LocalSend: too many announcements, {Peer} not answered", peer.Label);
                return;
            }
            try
            {
                registered = await _registerBack(peer, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException or JsonException)
            {
                _log.LogDebug(ex, "LocalSend: register back to {Peer} failed", peer.Label);
            }
            finally
            {
                Interlocked.Decrement(ref _registering);
            }
        }
        if (!registered)
            await SendAsync(announce: false, cancellationToken);
    }

    private async Task SendAsync(bool announce, CancellationToken cancellationToken)
    {
        if (_udp is not { } udp)
            return;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(_self with { Announce = announce }, LocalSendJsonContext.Default.LocalSendInfo);
        var target = new IPEndPoint(_group, _port);
        // Gửi qua từng card mạng: máy nhiều card (VPN, máy ảo) không chắc card mặc định là Wi-Fi.
        foreach (var local in ShareAddresses.LocalIPv4().DefaultIfEmpty(IPAddress.Loopback))
        {
            try
            {
                udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.GetAddressBytes());
                await udp.SendAsync(bytes, target, cancellationToken);
            }
            catch (SocketException ex)
            {
                _log.LogDebug(ex, "LocalSend: announce via {Address} failed", local);
            }
        }
    }

    private async Task ReceiveLoopAsync(UdpClient udp, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult packet;
            try
            {
                packet = await udp.ReceiveAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                return;
            }
            catch (SocketException ex)
            {
                _log.LogDebug(ex, "LocalSend: receive failed");
                continue;
            }
            _ = HandleSafelyAsync(packet, cancellationToken);
        }
    }

    private async Task HandleSafelyAsync(UdpReceiveResult packet, CancellationToken cancellationToken)
    {
        try
        {
            await HandlePacketAsync(packet.Buffer, packet.RemoteEndPoint, cancellationToken);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.LogDebug(ex, "LocalSend: packet from {Address} ignored", packet.RemoteEndPoint.Address);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;
        await _stop.CancelAsync();
        _udp?.Dispose();
        if (_loop is not null)
        {
            try
            {
                await _loop;
            }
            catch (OperationCanceledException)
            {
            }
        }
        _stop.Dispose();
    }
}
