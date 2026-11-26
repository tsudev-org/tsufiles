// TsuFiles: kiểm thử ngẫu nhiên (fuzz theo đột biến) cho các bộ đọc định dạng tự viết - đầu vào là tệp người dùng
// mở, nên là bề mặt tấn công chính của ứng dụng (docs/vi/KIEN-TRUC-BAO-MAT.md mục 2).
//
// Mỗi mục tiêu nhận các bản đột biến của một tệp hợp lệ (lật bit, ghi đè byte, chèn/xóa đoạn, số "nguy hiểm" như
// 0xFFFFFFFF ở vị trí ngẫu nhiên). Bộ đọc ĐƯỢC từ chối tệp hỏng bằng ngoại lệ thường, nhưng KHÔNG được:
//   - treo (quá TimeBudget mỗi tệp)              -> từ chối dịch vụ
//   - cấp phát quá AllocationBudget mỗi tệp      -> "bom" bộ nhớ
//   - tràn ngăn xếp / lỗi truy cập vùng nhớ      -> sập tiến trình (test host chết, CI báo đỏ)
//   - ném lỗi lập trình (NullReference, IndexOutOfRange, ...) -> dấu hiệu đọc ngoài biên chưa kiểm
// Seed cố định: lỗi tái hiện được. Số vòng: biến môi trường TSUDEV_FUZZ_ITERATIONS (mặc định 200 mỗi mục tiêu;
// workflow fuzz hằng tuần chạy nhiều hơn). ArchiveSafety được kiểm bằng thuộc tính: mọi đường dẫn trả về phải nằm
// trong thư mục gốc.

using System.IO.Compression;
using System.Text;
using TsuFiles.Engines.Archives;
using TsuFiles.Engines.Ebooks;
using TsuFiles.Engines.Icns;

namespace TsuFiles.Engines.Tests;

public sealed class ForkFuzzTests : IDisposable
{
    private static readonly TimeSpan TimeBudget = TimeSpan.FromSeconds(10);
    private const long AllocationBudget = 512L * 1024 * 1024;

    private static int Iterations =>
        int.TryParse(Environment.GetEnvironmentVariable("TSUDEV_FUZZ_ITERATIONS"), out var n) && n > 0 ? n : 200;

    private readonly string _folder = Directory.CreateTempSubdirectory("tsudev-fuzz-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ───────────────────────── Mục tiêu ─────────────────────────

    [Fact]
    public void PalmDoc_decompression_survives_random_input()
    {
        var seed = EbookBuilders.PalmDocCompress(Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("tsudev fuzz ", 400))));
        Fuzz(nameof(PalmDocCompression), seed, data => PalmDocCompression.Decompress(data));
    }

    [Fact]
    public void Palm_database_survives_mutations()
    {
        var seed = EbookBuilders.PalmDb("fuzz", "BOOKMOBI", [new byte[64], Encoding.ASCII.GetBytes("record one"), new byte[300]]);
        Fuzz(nameof(PalmDatabase), seed, data =>
        {
            var db = PalmDatabase.Open(data);
            for (var i = 0; i < Math.Min(db.Count, 64); i++) _ = db.Record(i).Length;
        });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(17480)]
    public void Mobi_reader_survives_mutations(int compression)
    {
        var seed = EbookBuilders.Mobi("<html><body><p>tsudev <b>fuzz</b></p><mbp:pagebreak/><p>hai</p></body></html>", [],
            new EbookBuilders.MobiOptions(Compression: compression));
        FuzzFile("mobi", seed, path => MobiReader.ReadBook(path, _folder));
    }

    [Fact]
    public void Icns_parser_and_decoders_survive_mutations()
    {
        var argb = new byte[4 + 16 * 16 * 4];
        "ARGB"u8.CopyTo(argb);
        var seed = IcnsFile.Write([new IcnsEntry("ic04", argb), new IcnsEntry("is32", new byte[16 * 16 * 3]), new IcnsEntry("s8mk", new byte[256])]);
        Fuzz("ICNS", seed, data =>
        {
            foreach (var candidate in IcnsFile.Candidates(IcnsFile.Parse(data)))
                _ = candidate.Decode?.Invoke();
        });
    }

    [Fact]
    public void Archive_entry_paths_never_resolve_outside_the_root()
    {
        var root = Directory.CreateDirectory(Path.Combine(_folder, "root")).FullName;
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        string[] pieces = ["..", ".", "a", "b.txt", "", " ", "...", "..∕", "C:", "\\\\?\\", "//", "\\", "%2e%2e", "\0",
            "con", "a:b", "~", "‮", "x\ty", "..%2f", "../", "..\\", "/etc", "\\\\server\\share"];
        var rng = new Random(0xA5C);
        for (var i = 0; i < Iterations * 25; i++)
        {
            var name = string.Join(rng.Next(3) switch { 0 => "/", 1 => "\\", _ => "" },
                Enumerable.Range(0, rng.Next(1, 7)).Select(_ => pieces[rng.Next(pieces.Length)]));
            string resolved;
            try
            {
                resolved = ArchiveSafety.ResolveEntryPath(root, name, "fuzz.zip");
            }
            catch (Exception ex) when (ex is not (NullReferenceException or IndexOutOfRangeException))
            {
                continue;   // từ chối là đúng
            }
            Assert.True(resolved.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase),
                $"'{Escape(name)}' -> '{resolved}' nằm ngoài {fullRoot}");
            Assert.False(ArchiveSafety.IsUnsafePath(name), $"'{Escape(name)}' bị coi là không an toàn nhưng vẫn được giải");
        }
    }

    // ───────────────────────── Bộ máy fuzz ─────────────────────────

    private void FuzzFile(string extension, byte[] seed, Action<string> read)
    {
        var path = Path.Combine(_folder, "input." + extension);
        Fuzz(extension, seed, data =>
        {
            File.WriteAllBytes(path, data);
            read(path);
        });
    }

    private static void Fuzz(string target, byte[] seed, Action<byte[]> read)
    {
        Run($"{target} seed", seed, () => read(seed));   // tệp gốc phải đọc được, nếu không fuzz vô nghĩa
        var rng = new Random(target.Aggregate(17, (h, c) => h * 31 + c));
        for (var i = 0; i < Iterations; i++)
        {
            var data = Mutate(seed, rng);
            Run($"{target} #{i}", data, () => read(data), seedMustPass: false);
        }
    }

    /// <summary>Lỗi lập trình: bộ đọc đọc ngoài biên/không kiểm null thay vì từ chối tệp hỏng.</summary>
    private static bool IsBug(Exception ex) => ex is NullReferenceException or IndexOutOfRangeException
        or AccessViolationException or InsufficientExecutionStackException or OutOfMemoryException
        or StackOverflowException or InvalidCastException or DivideByZeroException;

    private static void Run(string label, byte[] input, Action read, bool seedMustPass = true)
    {
        var task = Task.Run(() =>
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                read();
                return (Error: (Exception?)null, Allocated: GC.GetAllocatedBytesForCurrentThread() - before);
            }
            catch (Exception ex)
            {
                return (Error: ex, Allocated: GC.GetAllocatedBytesForCurrentThread() - before);
            }
        });
        if (!task.Wait(TimeBudget))
            Assert.Fail($"{label}: treo quá {TimeBudget.TotalSeconds} giây. {Repro(input)}");
        var (error, allocated) = task.Result;
        Assert.True(allocated <= AllocationBudget, $"{label}: cấp phát {allocated / (1024 * 1024)} MiB. {Repro(input)}");
        if (error is null) return;
        if (seedMustPass) Assert.Fail($"{label}: tệp gốc không đọc được: {error}");
        if (IsBug(error))
            Assert.Fail($"{label}: {error.GetType().Name} - {error.Message}\n{error.StackTrace}\n{Repro(input)}");
    }

    private static string Repro(byte[] input) => input.Length <= 4096
        ? "Đầu vào (base64): " + Convert.ToBase64String(input)
        : $"Đầu vào {input.Length} byte, SHA-256 {Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input))}";

    private static string Escape(string s) => string.Concat(s.Select(c => char.IsControl(c) || c > 0x7E ? $"\\u{(int)c:x4}" : c.ToString()));

    private static readonly uint[] Interesting = [0, 1, 0x7F, 0x80, 0xFF, 0x7FFF, 0x8000, 0xFFFF, 0x7FFFFFFF, 0x80000000, 0xFFFFFFFF];

    internal static byte[] Mutate(byte[] seed, Random rng)
    {
        var data = new List<byte>(seed);
        var rounds = 1 + rng.Next(8);
        for (var r = 0; r < rounds; r++)
        {
            var at = data.Count == 0 ? 0 : rng.Next(data.Count);
            switch (rng.Next(7))
            {
                case 0 when data.Count > 0: data[at] ^= (byte)(1 << rng.Next(8)); break;
                case 1 when data.Count > 0: data[at] = (byte)rng.Next(256); break;
                case 2 when data.Count > 4:
                    var value = Interesting[rng.Next(Interesting.Length)];
                    var start = Math.Min(at, data.Count - 4);
                    var bigEndian = rng.Next(2) == 0;
                    for (var k = 0; k < 4; k++)
                        data[start + k] = (byte)(value >> (8 * (bigEndian ? 3 - k : k)));
                    break;
                case 3: data.InsertRange(at, Enumerable.Range(0, rng.Next(1, 64)).Select(_ => (byte)rng.Next(256))); break;
                case 4 when data.Count > 1: data.RemoveRange(at, Math.Min(data.Count - at, rng.Next(1, 64))); break;
                case 5 when data.Count > 8:   // nhân đôi một đoạn (lặp cấu trúc)
                    var len = Math.Min(data.Count - at, rng.Next(1, 256));
                    data.InsertRange(rng.Next(data.Count), data.GetRange(at, len));
                    break;
                case 6 when data.Count > 1: data.RemoveRange(at, data.Count - at); break;   // cắt cụt
            }
        }
        return [.. data];
    }
}
