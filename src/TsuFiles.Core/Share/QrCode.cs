// Bộ sinh mã QR tự viết cho đường dẫn chia sẻ (ADR-0004 S2): chế độ byte, mức sửa lỗi M, phiên bản 1-40.
// Không thêm gói NuGet. Đối chiếu từng ô với segno 1.6.6 (tests/TsuFiles.Core.Tests/QrCodeTests.cs) và giải mã
// thử bằng OpenCV (tools/qr/doi-chieu-qr.py). Thuật toán theo ISO/IEC 18004:2015.

using System.Globalization;
using System.Text;

namespace TsuFiles.Core.Share;

public sealed class QrCode
{
    // Mức M: số byte sửa lỗi mỗi khối và số khối, chỉ số theo phiên bản (ISO/IEC 18004 bảng 9).
    private static readonly int[] EccPerBlock =
    [
        -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26,
        26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
    ];

    private static readonly int[] Blocks =
    [
        -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16,
        17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49,
    ];

    private readonly bool[,] _dark;
    private readonly bool[,] _function;

    private QrCode(int version)
    {
        Version = version;
        Size = version * 4 + 17;
        _dark = new bool[Size, Size];
        _function = new bool[Size, Size];
    }

    public int Version { get; }

    public int Size { get; }

    public int Mask { get; private set; }

    /// <summary>Ô (cột <paramref name="x"/>, hàng <paramref name="y"/>) tối; ngoài mã là sáng.</summary>
    public bool this[int x, int y] => x >= 0 && y >= 0 && x < Size && y < Size && _dark[y, x];

    public static QrCode Encode(string text) => Encode(Encoding.UTF8.GetBytes(text));

    public static QrCode Encode(byte[] data, int? mask = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (mask is < 0 or > 7)
            throw new ArgumentOutOfRangeException(nameof(mask));
        var version = 1;
        for (; ; version++)
        {
            if (version > 40)
                throw new ArgumentException("Dữ liệu quá dài cho mã QR.", nameof(data));
            if (4 + CountBits(version) + data.Length * 8 <= DataCodewords(version) * 8)
                break;
        }

        var bits = new List<bool>();
        Append(bits, 0b0100, 4);
        Append(bits, data.Length, CountBits(version));
        foreach (var b in data)
            Append(bits, b, 8);
        var capacity = DataCodewords(version) * 8;
        Append(bits, 0, Math.Min(4, capacity - bits.Count));
        Append(bits, 0, (8 - bits.Count % 8) % 8);
        for (var pad = 0xEC; bits.Count < capacity; pad ^= 0xEC ^ 0x11)
            Append(bits, pad, 8);

        var codewords = new byte[bits.Count / 8];
        for (var i = 0; i < bits.Count; i++)
        {
            if (bits[i])
                codewords[i >> 3] |= (byte)(1 << (7 - (i & 7)));
        }

        var qr = new QrCode(version);
        qr.DrawFunctionPatterns();
        qr.DrawCodewords(Interleave(version, codewords));

        if (mask is { } chosen)
        {
            qr.ApplyMask(chosen);
            qr.DrawFormat(chosen);
            qr.Mask = chosen;
            return qr;
        }

        var best = 0;
        var bestPenalty = int.MaxValue;
        for (var m = 0; m < 8; m++)
        {
            qr.ApplyMask(m);
            qr.DrawFormat(m);
            var penalty = qr.Penalty();
            if (penalty < bestPenalty)
            {
                best = m;
                bestPenalty = penalty;
            }
            qr.ApplyMask(m); // XOR lần nữa = gỡ mặt nạ
        }
        qr.ApplyMask(best);
        qr.DrawFormat(best);
        qr.Mask = best;
        return qr;
    }

    /// <summary>Ảnh SVG (1 đơn vị = 1 ô) có viền sáng <paramref name="border"/> ô, dùng cho trang web và tệp.</summary>
    public string ToSvg(int border = 4)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(border);
        var total = Size + border * 2;
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {total} {total}\" shape-rendering=\"crispEdges\">");
        svg.Append("<rect width=\"100%\" height=\"100%\" fill=\"#fff\"/><path fill=\"#000\" d=\"");
        svg.Append(ToPathData(border));
        svg.Append("\"/></svg>");
        return svg.ToString();
    }

    /// <summary>Dữ liệu đường vẽ (SVG/Avalonia) các ô tối, mỗi ô một hình vuông 1x1, dịch <paramref name="border"/> ô.</summary>
    public string ToPathData(int border = 0)
    {
        var path = new StringBuilder();
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (_dark[y, x])
                    path.Append(CultureInfo.InvariantCulture, $"M{x + border},{y + border}h1v1h-1z");
            }
        }
        return path.ToString();
    }

    private static int CountBits(int version) => version <= 9 ? 8 : 16;

    private static int RawDataModules(int version)
    {
        var result = (16 * version + 128) * version + 64;
        if (version >= 2)
        {
            var align = version / 7 + 2;
            result -= (25 * align - 10) * align - 55;
            if (version >= 7)
                result -= 36;
        }
        return result;
    }

    private static int DataCodewords(int version) =>
        RawDataModules(version) / 8 - EccPerBlock[version] * Blocks[version];

    private static void Append(List<bool> bits, int value, int count)
    {
        for (var i = count - 1; i >= 0; i--)
            bits.Add(((value >> i) & 1) != 0);
    }

    private static byte[] Interleave(int version, byte[] data)
    {
        var blockCount = Blocks[version];
        var eccLength = EccPerBlock[version];
        var raw = RawDataModules(version) / 8;
        var shortBlocks = blockCount - raw % blockCount;
        var shortLength = raw / blockCount;
        var divisor = Generator(eccLength);

        var blocks = new byte[blockCount][];
        for (int i = 0, k = 0; i < blockCount; i++)
        {
            var dataLength = shortLength - eccLength + (i < shortBlocks ? 0 : 1);
            var block = new byte[shortLength + 1];
            Array.Copy(data, k, block, 0, dataLength);
            k += dataLength;
            var ecc = Remainder(data.AsSpan(k - dataLength, dataLength), divisor);
            // Khối ngắn chừa một ô trống ở cuối phần dữ liệu để các khối thẳng hàng khi xen kẽ.
            Array.Copy(ecc, 0, block, i < shortBlocks ? dataLength + 1 : dataLength, eccLength);
            blocks[i] = block;
        }

        var result = new List<byte>(raw);
        for (var i = 0; i <= shortLength; i++)
        {
            for (var j = 0; j < blockCount; j++)
            {
                if (i != shortLength - eccLength || j >= shortBlocks)
                    result.Add(blocks[j][i]);
            }
        }
        return [.. result];
    }

    private static byte[] Generator(int degree)
    {
        var result = new byte[degree];
        result[degree - 1] = 1;
        byte root = 1;
        for (var i = 0; i < degree; i++)
        {
            for (var j = 0; j < degree; j++)
            {
                result[j] = Multiply(result[j], root);
                if (j + 1 < degree)
                    result[j] ^= result[j + 1];
            }
            root = Multiply(root, 0x02);
        }
        return result;
    }

    private static byte[] Remainder(ReadOnlySpan<byte> data, byte[] divisor)
    {
        var result = new byte[divisor.Length];
        foreach (var b in data)
        {
            var factor = (byte)(b ^ result[0]);
            Array.Copy(result, 1, result, 0, result.Length - 1);
            result[^1] = 0;
            for (var i = 0; i < result.Length; i++)
                result[i] ^= Multiply(divisor[i], factor);
        }
        return result;
    }

    private static byte Multiply(byte x, byte y)
    {
        var z = 0;
        for (var i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((y >> i) & 1) * x;
        }
        return (byte)z;
    }

    private void Set(int x, int y, bool dark)
    {
        _dark[y, x] = dark;
        _function[y, x] = true;
    }

    private void DrawFunctionPatterns()
    {
        for (var i = 0; i < Size; i++)
        {
            Set(6, i, i % 2 == 0);
            Set(i, 6, i % 2 == 0);
        }
        DrawFinder(3, 3);
        DrawFinder(Size - 4, 3);
        DrawFinder(3, Size - 4);

        var positions = AlignmentPositions();
        var last = positions.Length - 1;
        for (var i = 0; i < positions.Length; i++)
        {
            for (var j = 0; j < positions.Length; j++)
            {
                if ((i == 0 && j == 0) || (i == 0 && j == last) || (i == last && j == 0))
                    continue;
                for (var dy = -2; dy <= 2; dy++)
                {
                    for (var dx = -2; dx <= 2; dx++)
                        Set(positions[i] + dx, positions[j] + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
                }
            }
        }

        DrawFormat(0); // giữ chỗ, vẽ lại sau khi chọn mặt nạ
        if (Version >= 7)
        {
            var rem = Version;
            for (var i = 0; i < 12; i++)
                rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
            var bits = (Version << 12) | rem;
            for (var i = 0; i < 18; i++)
            {
                var dark = ((bits >> i) & 1) != 0;
                var a = Size - 11 + i % 3;
                var b = i / 3;
                Set(a, b, dark);
                Set(b, a, dark);
            }
        }
    }

    private void DrawFinder(int cx, int cy)
    {
        for (var dy = -4; dy <= 4; dy++)
        {
            for (var dx = -4; dx <= 4; dx++)
            {
                var x = cx + dx;
                var y = cy + dy;
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                if (x >= 0 && x < Size && y >= 0 && y < Size)
                    Set(x, y, distance != 2 && distance != 4);
            }
        }
    }

    private int[] AlignmentPositions()
    {
        if (Version == 1)
            return [];
        var count = Version / 7 + 2;
        var step = (Version * 8 + count * 3 + 5) / (count * 4 - 4) * 2;
        var result = new int[count];
        result[0] = 6;
        for (int i = count - 1, position = Size - 7; i >= 1; i--, position -= step)
            result[i] = position;
        return result;
    }

    private void DrawFormat(int mask)
    {
        // Mức M = 00; mã BCH(15,5) rồi XOR 0x5412.
        var data = mask;
        var rem = data;
        for (var i = 0; i < 10; i++)
            rem = (rem << 1) ^ ((rem >> 9) * 0x537);
        var bits = ((data << 10) | rem) ^ 0x5412;
        bool Bit(int i) => ((bits >> i) & 1) != 0;

        for (var i = 0; i <= 5; i++)
            Set(8, i, Bit(i));
        Set(8, 7, Bit(6));
        Set(8, 8, Bit(7));
        Set(7, 8, Bit(8));
        for (var i = 9; i < 15; i++)
            Set(14 - i, 8, Bit(i));

        for (var i = 0; i < 8; i++)
            Set(Size - 1 - i, 8, Bit(i));
        for (var i = 8; i < 15; i++)
            Set(8, Size - 15 + i, Bit(i));
        Set(8, Size - 8, true);
    }

    private void DrawCodewords(byte[] data)
    {
        var i = 0;
        for (var right = Size - 1; right >= 1; right -= 2)
        {
            if (right == 6)
                right = 5;
            for (var vertical = 0; vertical < Size; vertical++)
            {
                for (var j = 0; j < 2; j++)
                {
                    var x = right - j;
                    var upward = ((right + 1) & 2) == 0;
                    var y = upward ? Size - 1 - vertical : vertical;
                    if (!_function[y, x] && i < data.Length * 8)
                    {
                        _dark[y, x] = ((data[i >> 3] >> (7 - (i & 7))) & 1) != 0;
                        i++;
                    }
                }
            }
        }
    }

    private void ApplyMask(int mask)
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var invert = mask switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (x + y) % 3 == 0,
                    4 => (x / 3 + y / 2) % 2 == 0,
                    5 => x * y % 2 + x * y % 3 == 0,
                    6 => (x * y % 2 + x * y % 3) % 2 == 0,
                    _ => ((x + y) % 2 + x * y % 3) % 2 == 0,
                };
                if (invert && !_function[y, x])
                    _dark[y, x] = !_dark[y, x];
            }
        }
    }

    // Điểm phạt theo ISO/IEC 18004 mục 7.8.3 (N1=3, N2=3, N3=40, N4=10).
    private int Penalty()
    {
        var penalty = 0;
        for (var line = 0; line < Size; line++)
        {
            penalty += LinePenalty(i => _dark[line, i]);
            penalty += LinePenalty(i => _dark[i, line]);
        }

        var dark = 0;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (_dark[y, x])
                    dark++;
                if (x + 1 < Size && y + 1 < Size && _dark[y, x] == _dark[y, x + 1]
                    && _dark[y, x] == _dark[y + 1, x] && _dark[y, x] == _dark[y + 1, x + 1])
                    penalty += 3;
            }
        }
        var total = Size * Size;
        penalty += Math.Abs(dark * 20 - total * 10) / total * 10;
        return penalty;
    }

    private static readonly bool[] FinderLeft = [true, false, true, true, true, false, true, false, false, false, false];
    private static readonly bool[] FinderRight = [false, false, false, false, true, false, true, true, true, false, true];

    private int LinePenalty(Func<int, bool> at)
    {
        var penalty = 0;
        var run = 1;
        for (var i = 1; i <= Size; i++)
        {
            if (i < Size && at(i) == at(i - 1))
            {
                run++;
                continue;
            }
            if (run >= 5)
                penalty += 3 + run - 5;
            run = 1;
        }
        for (var i = 0; i + FinderLeft.Length <= Size; i++)
        {
            if (Matches(at, i, FinderLeft))
                penalty += 40;
            if (Matches(at, i, FinderRight))
                penalty += 40;
        }
        return penalty;
    }

    private static bool Matches(Func<int, bool> at, int start, bool[] pattern)
    {
        for (var k = 0; k < pattern.Length; k++)
        {
            if (at(start + k) != pattern[k])
                return false;
        }
        return true;
    }
}
