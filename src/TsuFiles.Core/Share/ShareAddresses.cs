// Địa chỉ IPv4 mạng nội bộ của máy để in vào mã QR (ADR-0004 S1). Bỏ loopback, card ảo không có cổng mặc định
// được xếp sau, chỉ giữ địa chỉ LocalNetwork coi là nội bộ (điện thoại cùng Wi-Fi mới tới được).

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TsuFiles.Core.Web;

namespace TsuFiles.Core.Share;

public static class ShareAddresses
{
    public static IReadOnlyList<IPAddress> LocalIPv4()
    {
        var found = new List<(IPAddress Address, bool Gateway)>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;
                var properties = nic.GetIPProperties();
                var gateway = properties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork
                                                                   && !g.Address.Equals(IPAddress.Any));
                found.AddRange(properties.UnicastAddresses
                    .Select(u => u.Address)
                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork && Usable(a))
                    .Select(a => (a, gateway)));
            }
        }
        catch (NetworkInformationException)
        {
        }
        return found.OrderByDescending(f => f.Gateway).Select(f => f.Address).Distinct().ToList();
    }

    /// <summary>Trong mạng nội bộ nhưng không phải loopback, link-local hay "this network".</summary>
    internal static bool Usable(IPAddress address)
    {
        var b = address.GetAddressBytes();
        return LocalNetwork.IsLocal(address) && b[0] is not (0 or 127) && !(b[0] == 169 && b[1] == 254) && b[0] < 224;
    }
}
