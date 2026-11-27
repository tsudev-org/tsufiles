// CMYK print files (issue #1027): PDFium draws a PDF or Illustrator file made in CMYK as RGB, the same as an RGB
// document (Ghostscript's CMYK rendering left with TG1 G3b). The documents are written with PDFsharp in CMYK colour
// mode, so their fills use the k operator.

using ImageMagick;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using TsuFiles.Core.Conversion;
using TsuFiles.Core.Presets;

namespace TsuFiles.Engines.Tests;

public class CmykRenderTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private static void AssertDone(ConversionJob job)
    {
        var errors = string.Join("; ", job.Files.Where(f => f.State == FileState.Failed).Select(f => $"{f.ErrorKey} {f.ErrorDetail}"));
        Assert.True(job.State == JobState.Completed, $"{job.State}: {errors}");
        Assert.All(job.Outputs, o => Assert.True(new FileInfo(o).Length > 0, o));
    }

    /// <summary>A PDF of one 200 x 100 pt page in two fills. In RGB mode PDFsharp writes the same colours as RGB.</summary>
    private static string MakePdf(string folder, string name, bool cmyk = true)
    {
        using var document = new PdfDocument();
        document.Options.ColorMode = cmyk ? PdfColorMode.Cmyk : PdfColorMode.Rgb;
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(200);
        page.Height = XUnit.FromPoint(100);
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            gfx.DrawRectangle(new XSolidBrush(XColor.FromCmyk(0.1, 0.2, 0.3, 0.4)), 0, 0, 100, 100);
            gfx.DrawRectangle(new XSolidBrush(XColor.FromCmyk(0.8, 0, 0.6, 0.1)), 100, 0, 100, 100);
        }
        var path = Path.Combine(folder, name);
        document.Save(path);
        return path;
    }

    [Fact]
    public async Task Rgb_pdf_still_becomes_an_rgb_jpg()
    {
        var dir = fx.NewFolder();
        var pdf = MakePdf(dir, "screen.pdf", cmyk: false);

        var job = await fx.ConvertAsync([pdf], new Preset { TargetFormat = "jpg", Pdf = { RenderDpi = 72 } });

        AssertDone(job);
        using var image = new MagickImage(job.Outputs.Single());
        Assert.Equal(ColorSpace.sRGB, image.ColorSpace);
        Assert.Equal((200u, 100u), (image.Width, image.Height));
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpg")]
    public async Task Cmyk_pdf_becomes_an_rgb_picture_without_a_notice(string target)
    {
        var dir = fx.NewFolder();
        var pdf = MakePdf(dir, "print.pdf");

        var job = await fx.ConvertAsync([pdf], new Preset { TargetFormat = target, Pdf = { RenderDpi = 72 } });

        AssertDone(job);
        using var image = new MagickImage(job.Outputs.Single());
        Assert.Equal(ColorSpace.sRGB, image.ColorSpace);
        Assert.Equal((200u, 100u), (image.Width, image.Height));
        Assert.Null(job.Files.Single().NoticeKey);
    }

    [Fact]
    public async Task Grayscale_preset_keeps_pdfium_for_cmyk_pages()
    {
        var dir = fx.NewFolder();
        var pdf = MakePdf(dir, "print.pdf");

        var job = await fx.ConvertAsync([pdf], new Preset { TargetFormat = "jpg", Pdf = { RenderDpi = 72 }, Image = { Grayscale = true } });

        AssertDone(job);
        using var image = new MagickImage(job.Outputs.Single());
        Assert.NotEqual(ColorSpace.CMYK, image.ColorSpace);
    }

    [Theory]
    [InlineData("ai", "jpg", "vector,pdfium")]
    [InlineData("ai", "tiff", "vector,pdfium")]
    [InlineData("pdf", "jpg", "pdfium")]
    public void Print_files_are_drawn_by_pdfium(string from, string to, string engines)
    {
        var route = fx.Catalog.CreatePlanner().Plan(from, to);

        Assert.NotNull(route);
        Assert.Equal(engines, string.Join(',', route.Steps.Select(s => s.Converter.Id)));
    }
}
