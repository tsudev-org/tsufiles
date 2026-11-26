// A scanned PDF → DOCX with its text recognized (PdfOptions.Ocr) and the exact layout: the recognized words become
// editable text at their place, the scan behind them loses those words' strokes and keeps everything else
// (PdfDocumentReader.Recognized, ScanText).

using SkiaSharp;
using TsuFiles.Engines.Hwp.Hwpx;
using TsuFiles.Engines.Pdf;

namespace TsuFiles.Engines.Tests;

public class PdfScanExactTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    /// <summary>Pixels per point of the fake scan.</summary>
    private const int Scale = 2;

    /// <summary>
    /// A page "scanned" at 144 dpi: dark strokes where the words are (13 pt words on the baseline at 200 pt), a red seal
    /// across the second word, a table rule below; Tesseract's invisible words on top.
    /// </summary>
    private string Scan(string folder)
    {
        using var bitmap = new SKBitmap(595 * Scale, 842 * Scale);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(250, 249, 245));
            using var ink = new SKPaint { Color = new SKColor(40, 40, 40) };
            // Upright strokes, 2 px wide, as letters are: inside the boxes of "Hello" (100-131 pt) and "world" (135-168 pt).
            foreach (var (from, to) in new[] { (101, 130), (136, 167) })
            {
                for (var x = from; x < to; x += 3)
                    canvas.DrawRect(x * Scale, 191 * Scale, 2, 8 * Scale, ink);
            }
            using var seal = new SKPaint { Color = new SKColor(220, 30, 30), Style = SKPaintStyle.Stroke, StrokeWidth = 3 };
            canvas.DrawCircle(150 * Scale, 195 * Scale, 14 * Scale, seal);
            canvas.DrawRect(60 * Scale, 230 * Scale, 475 * Scale, 2, ink);
        }
        var png = Path.Combine(folder, "scan.png");
        using (var data = bitmap.Encode(SKEncodedImageFormat.Png, 100))
            File.WriteAllBytes(png, data.ToArray());

        var pdf = new PdfBuilder().NewPage();
        pdf.Picture(png, 0, 0, 595, 842);
        pdf.Invisible("Hello", 13, pdf.Font(), 100, 200);
        pdf.Invisible("world", 13, pdf.Font(), 135, 200);
        return pdf.Save(Path.Combine(folder, "scan.pdf"));
    }

    [Fact]
    public void Recognized_words_are_text_on_the_scan_without_their_strokes()
    {
        var path = Scan(fx.NewFolder());

        var document = PdfDocumentReader.ReadExact(path, Path.Combine(fx.NewFolder(), "media"), ocrLayer: true);

        var holder = Assert.IsType<HParagraph>(Assert.Single(Assert.Single(document.Sections).Blocks));
        var box = Assert.Single(holder.Inlines.OfType<HTextBox>());
        var runs = box.Blocks.OfType<HParagraph>().Single().Inlines.OfType<HText>().ToList();
        Assert.Equal("Hello world", string.Concat(runs.Select(r => r.Text)));
        Assert.All(runs, r => Assert.Equal("Times New Roman", r.Format.Font));
        Assert.All(runs.Where(r => r.Text.Trim().Length > 0), r => Assert.Null(r.Format.Color)); // black print

        var background = Assert.IsType<HImage>(holder.Inlines[0]);
        using var picture = SKBitmap.Decode(background.Path);
        var scale = picture.Width / 595.0;
        SKColor At(double x, double y) => picture.GetPixel((int)Math.Round(x * scale), (int)Math.Round(y * scale));
        // The words' strokes are gone, the seal and the rule stay.
        for (var x = 101.5; x < 166; x += 3)
            Assert.True(At(x, 195).Red > 200 || At(x, 195).Red - At(x, 195).Green > 80, $"stroke left at {x}");
        Assert.True(At(150, 181).Red - At(150, 181).Green > 80, "the seal");
        Assert.True(At(300, 230.4).Red < 120, "the rule");
    }

    [Fact]
    public void Without_ocr_a_scan_is_the_picture_only()
    {
        var path = Scan(fx.NewFolder());

        var holder = (HParagraph)PdfDocumentReader.ReadExact(path, Path.Combine(fx.NewFolder(), "media")).Sections[0].Blocks.Single();

        Assert.Empty(holder.Inlines.OfType<HTextBox>());
        Assert.Single(holder.Inlines.OfType<HImage>());
    }

    [Theory]
    [InlineData("I1", null, "11")]
    [InlineData("2O26", null, "2O26")]
    [InlineData("Il", "tháng", "11")]
    [InlineData("Il", "Số:", "11")]
    [InlineData("Il", "Ông", "Il")]
    [InlineData("1|0", null, "110")]
    [InlineData("2026", "năm", "2026")]
    [InlineData("Ill", null, "Ill")]
    public void Ones_read_as_letters_in_numbers_become_ones(string text, string? previous, string expected) =>
        Assert.Equal(expected, ScanText.FixDigits(text, previous));

    [Fact]
    public void Leaning_strokes_read_as_italic_and_upright_ones_do_not()
    {
        // Ten vertical strokes 12 px tall, upright or leaning right by 0.21 per pixel of height.
        List<(int X, int Y)> Strokes(double lean) =>
            [.. Enumerable.Range(0, 10).SelectMany(s => Enumerable.Range(0, 12).Select(y => ((int)Math.Round(s * 6 + 2 + lean * y), y)))];

        Assert.True(ScanText.Slant(Strokes(0.21), 64, 12) > 1.12);
        Assert.True(ScanText.Slant(Strokes(0), 64, 12) < 1.12);
        Assert.Equal(1, ScanText.Slant([(1, 1)], 4, 4));
    }
}
