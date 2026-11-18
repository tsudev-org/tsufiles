// "tsufiles send" (ADR-0004 S3): chọn máy nhận theo tên hay địa chỉ IP.

using System.Net;
using TsuFiles.Core.Share.LocalSend;

namespace TsuFiles.Cli.Tests;

public sealed class SendCommandTests
{
    private static LocalSendPeer Peer(string alias, string ip, int port = 53317) =>
        new(IPAddress.Parse(ip), new LocalSendInfo { Alias = alias, Fingerprint = alias + port, Port = port }, DateTimeOffset.UtcNow);

    private static readonly LocalSendPeer[] Peers =
        [Peer("iPhone của Su", "192.168.1.5"), Peer("Nice Orange", "192.168.1.6"), Peer("iPhone", "192.168.1.7")];

    [Theory]
    [InlineData("192.168.1.6", "Nice Orange")]
    [InlineData("nice", "Nice Orange")]
    [InlineData("IPHONE", "iPhone")] // trùng đúng tên thắng khớp một phần
    [InlineData("của su", "iPhone của Su")]
    public void Picks_one_device_by_name_or_address(string to, string alias) =>
        Assert.Equal(alias, Assert.Single(SendCommand.Match(Peers, to)).Info.Alias);

    [Theory]
    [InlineData("192.168.1.99", 0)]
    [InlineData("máy lạ", 0)]
    [InlineData("i", 3)]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    public void Unknown_or_ambiguous_names_do_not_pick_a_device(string to, int count) =>
        Assert.Equal(count, SendCommand.Match(Peers, to).Count);

    [Fact]
    public void Two_tsufiles_on_one_computer_are_told_apart_by_port()
    {
        LocalSendPeer[] same = [Peer("TsuFiles (pc)", "192.168.1.5"), Peer("TsuFiles (pc)", "192.168.1.5", 40123)];

        Assert.Equal(2, SendCommand.Match(same, "192.168.1.5").Count);
        Assert.Equal(40123, Assert.Single(SendCommand.Match(same, "192.168.1.5:40123")).Port);
        Assert.Equal(53317, Assert.Single(SendCommand.Match(same, "192.168.1.5:53317")).Port);
        Assert.Empty(SendCommand.Match(same, "192.168.1.5:1"));
    }
}
