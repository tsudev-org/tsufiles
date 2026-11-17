// ADR-0004 S2: bộ sinh mã QR tự viết. Kết quả đối chiếu sinh bằng tools/qr/doi-chieu-qr.py (segno làm tham chiếu).

using System.Security.Cryptography;
using System.Text;
using TsuFiles.Core.Share;

namespace TsuFiles.Core.Tests;

public sealed class QrCodeTests
{
    private static byte[] Data(int n)
    {
        var data = new byte[n];
        for (var i = 0; i < n; i++)
            data[i] = (byte)((i * 31 + 7) & 0xFF);
        return data;
    }

    private static string Rows(QrCode qr)
    {
        var rows = new StringBuilder();
        for (var y = 0; y < qr.Size; y++)
        {
            if (y > 0)
                rows.Append('\n');
            for (var x = 0; x < qr.Size; x++)
                rows.Append(qr[x, y] ? '1' : '0');
        }
        return rows.ToString();
    }

    [Theory]
    [InlineData(1, 0, 1, "e28946b9fd1c59bc55478b0fa7a4ec5afa403792d5ca206483a307cefb914cfe")]
    [InlineData(14, 6, 1, "f2156d621e1c190b8df07d87dc53adccf83669da01d83d2b5c4423ea59193882")]
    [InlineData(15, 7, 2, "f66d4bb7e2b71811ffef6a9213c0210acb77f8062f7dcc8cc369096ef0834efa")]
    [InlineData(16, 1, 2, "9e3cd142b4f017417fa1a8e84d4a620a38803d8a20839b80b295134b673913ec")]
    [InlineData(30, 2, 3, "d108623ad011a277aae0a5c4a2e5fadce868d6cdda050a14a472139332ed4785")]
    [InlineData(60, 3, 4, "165c792e6e3f78d09b3db0561ec198d513cd072d86b935bf55494456c865526d")]
    [InlineData(100, 4, 6, "fa3839e4d389e420037ea5508cde096cdbf9cf34c99eaf62d746e2b1ba24f206")]
    [InlineData(154, 5, 9, "7cd7b856b43cea81fba1e4cfefed8e5dacc5dce47b683dba3a17b12d4ed65bf5")]
    [InlineData(200, 0, 10, "6d321d62ed4cd83e6fc8b54effb4d819268a8ad3dc18a072d8eb51a9f98b0187")]
    [InlineData(213, 7, 10, "d3d8e503164e0615bac3947072296fb5ce325e667fff1655aa1521a4c5118f13")]
    [InlineData(300, 4, 13, "58b476055a6e3deac050f643e782e40e7bff47147f46776563b4240a7ac93e8a")]
    [InlineData(400, 6, 15, "30f1f4869ddcae9f75e4deef69d5a0aa657829be8c1f15064fa3ff1eed3f281a")]
    [InlineData(500, 1, 17, "69eec7b94352b4b13d4a0600fb7d4c250957d690665e6206d6d48fe3dcd6825d")]
    [InlineData(700, 2, 21, "237c718bda8adb65ad871bba9e8f8b3b97e8b7c47f636276a49ff8b61a4a2b21")]
    [InlineData(1000, 3, 26, "1107cab767cb3fc725f4fab7d667ee76d46c05c08130ef262046a5b0fdedeb4c")]
    [InlineData(1300, 5, 30, "98c4223146cbfad2cfc8d0070cd506930f80069d17415263b7dca36f3b84ab4a")]
    [InlineData(1700, 6, 34, "506d3449a8bf2e3401e343d988b354bf489e9ae3a5cc221149150a3408eaa58d")]
    [InlineData(2000, 0, 38, "25dc8dc472300bd7c89daa9bff7f2d67e5039f30281d6722d4af366ddeffdd02")]
    [InlineData(2331, 7, 40, "f1af3a4a06c22a719b81c5e5e89ce35736246446d373726b2e3600f7717961aa")]
    public void Every_module_matches_the_reference_encoder(int length, int mask, int version, string sha256)
    {
        var qr = QrCode.Encode(Data(length), mask);
        Assert.Equal(version, qr.Version);
        Assert.Equal(version * 4 + 17, qr.Size);
        Assert.Equal(mask, qr.Mask);
        Assert.Equal(sha256, Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(Rows(qr)))));
    }

    [Fact]
    public void A_share_link_fits_a_small_code_a_phone_reads_from_across_the_desk()
    {
        var qr = QrCode.Encode("http://192.168.1.23:53817/s/0f9a3c6b2e1d4a5f8b7c6d5e4f3a2b1c/");
        Assert.InRange(qr.Version, 1, 5);
        Assert.InRange(qr.Mask, 0, 7);
        // Ba mắt định vị ở ba góc.
        foreach (var (x, y) in new[] { (0, 0), (qr.Size - 7, 0), (0, qr.Size - 7) })
        {
            Assert.True(qr[x, y] && qr[x + 6, y + 6] && qr[x + 3, y + 3]);
            Assert.False(qr[x + 1, y + 1]);
        }
        Assert.False(qr[-1, 0]);
        Assert.False(qr[qr.Size, 0]);
    }

    [Fact]
    public void Automatic_mask_matches_the_same_code_with_that_mask_forced()
    {
        var auto = QrCode.Encode(Data(120));
        Assert.Equal(Rows(QrCode.Encode(Data(120), auto.Mask)), Rows(auto));
    }

    [Fact]
    public void Too_much_data_is_refused()
    {
        Assert.Throws<ArgumentException>(() => QrCode.Encode(Data(2332)));
        Assert.Throws<ArgumentOutOfRangeException>(() => QrCode.Encode(Data(1), 8));
    }

    [Fact]
    public void Svg_has_a_white_quiet_zone_and_one_square_per_dark_module()
    {
        var qr = QrCode.Encode("TsuFiles");
        var svg = qr.ToSvg();
        var total = qr.Size + 8;
        Assert.StartsWith($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {total} {total}\"", svg);
        var dark = Rows(qr).Count(c => c == '1');
        Assert.Equal(dark, qr.ToPathData().Split('M', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("M4,4h1v1h-1z", svg);
    }
}
