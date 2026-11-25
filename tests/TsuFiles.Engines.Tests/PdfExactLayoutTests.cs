// PDF → DOCX with the exact layout (PdfOptions.WordLayout = Exact, the default): the page without its text as the
// picture behind, the words in text boxes at their place, the annotation layer in front (PdfDocumentReader.Exact,
// PdfHiddenText).

using System.Text;
using System.Xml.Linq;
using SkiaSharp;
using TsuFiles.Core.Conversion;
using TsuFiles.Core.Presets;
using TsuFiles.Engines.Hwp.Hwpx;
using TsuFiles.Engines.Pdf;
using static TsuFiles.Engines.Tests.DocxAssert;

namespace TsuFiles.Engines.Tests;

public class PdfExactLayoutTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private static readonly XNamespace Wps = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";

    /// <summary>A page with a heading in red, a ruled box around a line of text and a grey bar.</summary>
    private static string Form(string folder)
    {
        var pdf = new PdfBuilder().NewPage();
        pdf.Line("APPLICATION FORM", 16, pdf.Font(bold: true), 200, 80, (200, 0, 0));
        pdf.Rule(60, 120, 535, 120, 1).Rule(60, 160, 535, 160, 1).Rule(60, 120, 60, 160, 1).Rule(535, 120, 535, 160, 1);
        pdf.Line("Full name: Nguyen Van A", 12, pdf.Font(), 70, 145);
        pdf.Bar(60, 200, 475, 20, (128, 128, 128));
        return pdf.Save(Path.Combine(folder, "form.pdf"));
    }

    private async Task<string> ConvertAsync(string input, PdfWordLayout? layout = null)
    {
        var preset = new Preset { TargetFormat = "docx" };
        if (layout is { } chosen)
            preset.Pdf.WordLayout = chosen;
        var job = await fx.ConvertAsync([input], preset);
        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        return job.Outputs.Single();
    }

    [Fact]
    public void Hide_makes_every_text_object_invisible_and_keeps_other_bytes()
    {
        var content = "q 1 0 0 1 0 0 cm BT /F1 12 Tf (BT 2 Tr) Tj 0 Tr ET\n5 Tr\nBI /W 1 /H 1 /BPC 8 /CS /G ID \x01BT\x02 EI\nQ % BT in a comment\n"u8.ToArray();

        var hidden = Encoding.Latin1.GetString(PdfHiddenText.Hide(content));

        Assert.Equal("q 1 0 0 1 0 0 cm BT 3 Tr /F1 12 Tf (BT 2 Tr) Tj 3 Tr ET\n7 Tr\nBI /W 1 /H 1 /BPC 8 /CS /G ID \x01BT\x02 EI\nQ % BT in a comment\n", hidden);
    }

    [Fact]
    public void The_page_is_a_picture_without_text_behind_text_boxes_at_their_place()
    {
        var path = Form(fx.NewFolder());

        var document = PdfDocumentReader.ReadExact(path, Path.Combine(fx.NewFolder(), "media"));

        var section = Assert.Single(document.Sections);
        Assert.Equal(59500, section.Page!.Width);
        Assert.Equal(84200, section.Page.Height);
        var holder = Assert.IsType<HParagraph>(Assert.Single(section.Blocks));
        var background = Assert.IsType<HImage>(holder.Inlines[0]);
        Assert.Equal("BEHIND_TEXT", background.Anchor!.Wrap);
        Assert.Equal((0, 0), (background.Anchor.HorizontalOffset, background.Anchor.VerticalOffset));

        // The rules and the bar are in the picture, the words are not.
        using (var bitmap = SKBitmap.Decode(background.Path))
        {
            var scale = bitmap.Width / 595.0;
            SKColor At(double x, double y) => bitmap.GetPixel((int)(x * scale), (int)(y * scale));
            Assert.True(At(300, 120).Red < 128, "the top rule");
            Assert.True(At(60, 140).Red < 128, "the left rule");
            Assert.True(At(300, 210).Red is > 100 and < 160, "the grey bar");
            for (var x = 72.0; x < 200; x += 1)
                Assert.True(At(x, 141).Red > 240, $"no text at {x}");
        }

        var boxes = holder.Inlines.OfType<HTextBox>().ToList();
        Assert.Equal(2, boxes.Count);
        Assert.All(boxes, b => Assert.True(b.NoWrap && b.Line.Style == HBorderStyle.None && b.Fill is null && b.Anchor!.Wrap == "IN_FRONT_OF_TEXT"));
        string Text(HTextBox box) => string.Concat(box.Blocks.OfType<HParagraph>().SelectMany(p => p.Inlines).OfType<HText>().Select(t => t.Text));
        var heading = boxes.Single(b => Text(b) == "APPLICATION FORM");
        Assert.InRange(heading.Anchor!.HorizontalOffset, 19900, 20100);
        var headingRun = heading.Blocks.OfType<HParagraph>().Single().Inlines.OfType<HText>().First();
        Assert.Equal("#C80000", headingRun.Format.Color);
        Assert.True(headingRun.Format.Bold);
        Assert.Equal(1600, headingRun.Format.Size);
        var name = boxes.Single(b => Text(b) == "Full name: Nguyen Van A");
        Assert.InRange(name.Anchor!.HorizontalOffset, 6900, 7100);
        // The top of the box is above the baseline (145 pt down) by about the ascent, inside the ruled box.
        Assert.InRange(name.Anchor.VerticalOffset, 12000, 14500);
    }

    [Fact]
    public void A_page_of_text_only_has_no_picture()
    {
        var pdf = new PdfBuilder().NewPage();
        pdf.Line("Only words here", 12, pdf.Font(), 72, 100);
        var path = pdf.Save(Path.Combine(fx.NewFolder(), "words.pdf"));

        var holder = (HParagraph)PdfDocumentReader.ReadExact(path, Path.Combine(fx.NewFolder(), "media")).Sections[0].Blocks.Single();

        Assert.Empty(holder.Inlines.OfType<HImage>());
        Assert.Single(holder.Inlines.OfType<HTextBox>());
    }

    [Fact]
    public async Task Pdf_to_docx_is_exact_by_default_and_flowing_on_request()
    {
        var path = Form(fx.NewFolder());

        var exact = await ConvertAsync(path);
        ValidPackage(exact);
        var body = Body(exact);
        var bodyPr = body.Descendants(Wps + "bodyPr").Where(b => b.Parent!.Element(Wps + "txbx") is not null).ToList();
        Assert.Equal(2, bodyPr.Count);
        Assert.All(bodyPr, b => Assert.Equal("none", (string?)b.Attribute("wrap")));
        Assert.Contains(body.Descendants(Wp + "anchor"), a => (string?)a.Attribute("behindDoc") == "1");
        Assert.Contains("Nguyen Van A", string.Concat(body.Descendants(W + "t").Select(t => t.Value)), StringComparison.Ordinal);

        var flowing = await ConvertAsync(path, PdfWordLayout.Flowing);
        var flowingBody = Body(flowing);
        Assert.Empty(flowingBody.Descendants(Wps + "txbx"));
        Assert.Contains("Nguyen Van A", string.Concat(flowingBody.Descendants(W + "t").Select(t => t.Value)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_preset_saved_before_the_option_reads_as_exact()
    {
        Assert.Equal(PdfWordLayout.Exact, new PdfOptions().WordLayout);
        var options = System.Text.Json.JsonSerializer.Deserialize<PdfOptions>("""{"Ocr":false,"WordLayout":"Flowing"}""")!;
        Assert.Equal(PdfWordLayout.Flowing, options.WordLayout);
        Assert.Equal(PdfWordLayout.Exact, System.Text.Json.JsonSerializer.Deserialize<PdfOptions>("""{"WordLayout":"Unknown"}""")!.WordLayout);
    }
}
