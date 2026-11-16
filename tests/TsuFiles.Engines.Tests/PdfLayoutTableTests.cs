// PDF → DOCX of documents laid out with tables whose borders are hidden and signed with a digital certificate, as
// Vietnamese administrative documents are: the heading (issuing body | national motto, number | place and date) and
// the closing block (recipients | signer) become tables without borders, and the annotation layer (the signature, the
// seal, the stamped number) floats in front of the text at its place on the paper. The PDFs are drawn here with the
// standard 14 fonts; the annotation is a Widget with its own appearance, as signing tools write it.

using System.Globalization;
using System.Text;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SkiaSharp;
using TsuFiles.Core.Conversion;
using TsuFiles.Core.Presets;
using TsuFiles.Engines.Hwp.Hwpx;
using TsuFiles.Engines.Pdf;
using static TsuFiles.Engines.Tests.DocxAssert;

namespace TsuFiles.Engines.Tests;

public class PdfLayoutTableTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private async Task<string> ConvertAsync(string input, string target)
    {
        var job = await fx.ConvertAsync([input], new Preset { TargetFormat = target });
        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        return job.Outputs.Single();
    }

    /// <summary>A line centred on <paramref name="center"/>.</summary>
    private static void Centred(PdfBuilder pdf, string text, double size, bool bold, double center, double y)
    {
        var font = pdf.Font(bold);
        pdf.Line(text, size, font, center - pdf.Width(text, size, font) / 2, y);
    }

    /// <summary>
    /// An administrative letter: the two-column heading, the number next to the place and date, a body paragraph and
    /// the closing block with the recipients on the left and the signer on the right (a gap left for the signature).
    /// </summary>
    private static string Letter(string folder)
    {
        var pdf = new PdfBuilder().NewPage();
        Centred(pdf, "PROVINCE", 12, false, 150, 60);
        Centred(pdf, "DEPARTMENT OF INFORMATION", 12, true, 150, 75);
        Centred(pdf, "SOCIALIST REPUBLIC OF VIET NAM", 12, true, 400, 60);
        Centred(pdf, "Independence - Freedom", 12, true, 400, 75);
        var regular = pdf.Font();
        pdf.Line("No.:", 12, regular, 110, 100);
        pdf.Line("/OFF-IT", 12, regular, 160, 100);
        pdf.Line("City, day", 12, pdf.Font(italic: true), 330, 100);
        pdf.Line("month 9 year 2026", 12, pdf.Font(italic: true), 410, 100);
        pdf.Paragraph("The office asks every unit to review the list of officers and to register them for the training "
                      + "course before the date given below, and to report the result to the provincial committee.", 12, regular, 60, 150, 475, 16);
        pdf.Line("Recipients:", 11, pdf.Font(bold: true, italic: true), 60, 260);
        pdf.Line("- As above;", 10, regular, 60, 275);
        pdf.Line("- Archive: office.", 10, regular, 60, 288);
        Centred(pdf, "DIRECTOR", 12, true, 420, 260);
        Centred(pdf, "Nguyen Van A", 12, true, 420, 330);
        return pdf.Save(Path.Combine(folder, "letter.pdf"));
    }

    /// <summary>
    /// Adds a Widget annotation (a signature field) at <paramref name="rect"/> (points, PDF coordinates) whose
    /// appearance fills it with a red rectangle, as signing tools add the seal.
    /// </summary>
    private static void AddSeal(string path, double[] rect)
    {
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Modify);
        var page = document.Pages[0];
        var (width, height) = (rect[2] - rect[0], rect[3] - rect[1]);
        var appearance = new PdfDictionary(document);
        appearance.Elements.SetName("/Type", "/XObject");
        appearance.Elements.SetName("/Subtype", "/Form");
        appearance.Elements["/BBox"] = new PdfArray(document, new PdfReal(0), new PdfReal(0), new PdfReal(width), new PdfReal(height));
        appearance.CreateStream(Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"0.85 0.1 0.1 rg 0 0 {width} {height} re f")));
        document.Internals.AddObject(appearance);
        var states = new PdfDictionary(document);
        states.Elements["/N"] = appearance.Reference;
        var widget = new PdfDictionary(document);
        widget.Elements.SetName("/Type", "/Annot");
        widget.Elements.SetName("/Subtype", "/Widget");
        widget.Elements.SetName("/FT", "/Sig");
        widget.Elements.SetString("/T", "Signature1");
        widget.Elements.SetInteger("/F", 4 + 128); // Print, Locked
        widget.Elements["/Rect"] = new PdfArray(document, [.. rect.Select(v => new PdfReal(v))]);
        widget.Elements["/AP"] = states;
        document.Internals.AddObject(widget);
        page.Elements["/Annots"] = new PdfArray(document, widget.Reference!);
        document.Save(path);
    }

    private static List<HBlock> Blocks(HDocument document) =>
        [.. document.Sections.SelectMany(s => s.Blocks).Where(b => b is HTable || (b is HParagraph p && TextOf(p).Trim().Length > 0))];

    /// <summary>The cell's paragraphs, one per line, with line breaks as \n.</summary>
    private static string CellText(HCell cell) => string.Join("\n", Paragraphs(cell.Blocks).Select(Shown).Select(t => t.Trim()).Where(t => t.Length > 0));

    private static string Shown(HParagraph paragraph) =>
        string.Concat(paragraph.Inlines.Select(i => i switch { HText t => t.Text, HLineBreak => "\n", _ => "" }));

    [Fact]
    public void Heading_and_closing_block_become_tables_without_borders()
    {
        var path = Letter(fx.NewFolder());

        var blocks = Blocks(PdfDocumentReader.ReadLayout(path, Path.Combine(fx.NewFolder(), "media")));

        var heading = Assert.IsType<HTable>(blocks[0]);
        Assert.Equal(2, heading.ColumnCount);
        Assert.Equal(2, heading.Rows.Count);
        Assert.Equal(["PROVINCE\nDEPARTMENT OF INFORMATION", "SOCIALIST REPUBLIC OF VIET NAM\nIndependence - Freedom"],
            heading.Rows[0].Cells.Select(CellText).Select(t => t.Replace("\n\n", "\n", StringComparison.Ordinal)));
        // The blank for the number stays a blank in one line, not two paragraphs.
        var number = CellText(heading.Rows[1].Cells[0]);
        Assert.Matches("^No\\.: {4,}/OFF-IT$", number);
        Assert.Matches("^City, day {3,}month 9 year 2026$", CellText(heading.Rows[1].Cells[1]));
        Assert.All(heading.Rows.SelectMany(r => r.Cells), c => Assert.Equal(HBorders.Empty, c.Borders));

        Assert.StartsWith("The office asks every unit", TextOf(Assert.IsType<HParagraph>(blocks[1])).Trim());

        var closing = Assert.IsType<HTable>(blocks[2]);
        Assert.Equal(2, closing.ColumnCount);
        Assert.Equal("Recipients:\n- As above;\n- Archive: office.", CellText(closing.Rows[0].Cells[0]));
        // The signer's name under the room for the signature stays in the signer's column.
        Assert.Equal("DIRECTOR\nNguyen Van A", CellText(closing.Rows[0].Cells[1]));
        Assert.Equal(3, blocks.Count);
    }

    [Fact]
    public void Plain_text_and_markdown_keep_the_blocks_as_paragraphs()
    {
        var path = Letter(fx.NewFolder());

        Assert.Empty(PdfDocumentReader.Read(path, Path.Combine(fx.NewFolder(), "media")).Sections.SelectMany(s => s.Blocks).OfType<HTable>());
        var text = PdfDocumentReader.PlainText(PdfDocumentReader.ReadText(path));
        Assert.Contains("DEPARTMENT OF INFORMATION", text, StringComparison.Ordinal);
        Assert.Contains("Nguyen Van A", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Numbered_list_and_column_layout_stay_paragraphs()
    {
        var pdf = new PdfBuilder().NewPage();
        var regular = pdf.Font();
        // "1." with its text far to the right on the same line: a list, not a block side by side.
        pdf.Line("1.", 12, regular, 60, 80);
        pdf.Line("Review the list of officers.", 12, regular, 90, 80);
        pdf.Line("2.", 12, regular, 60, 100);
        pdf.Line("Register them for the course.", 12, regular, 90, 100);
        // Two columns of text over most of the page: a column layout for the reading order, not a table.
        for (var line = 0; line < 40; line++)
        {
            pdf.Line($"Left column line {line} of the article text", 10, regular, 60, 140 + line * 16);
            pdf.Line($"Right column line {line} of the article text", 10, regular, 320, 140 + line * 16);
        }
        var path = pdf.Save(Path.Combine(fx.NewFolder(), "columns.pdf"));

        var document = PdfDocumentReader.ReadLayout(path, Path.Combine(fx.NewFolder(), "media"));

        Assert.Empty(document.Sections.SelectMany(s => s.Blocks).OfType<HTable>());
    }

    [Fact]
    public void Signature_widget_floats_in_front_of_the_text_with_transparency()
    {
        var path = Letter(fx.NewFolder());
        // In PDF coordinates (y up): over the signer's title, 100 × 50 points.
        AddSeal(path, [370, 842 - 320, 470, 842 - 270]);

        var document = PdfDocumentReader.ReadLayout(path, Path.Combine(fx.NewFolder(), "media"));

        var floating = document.Sections.SelectMany(s => s.Blocks).OfType<HParagraph>().SelectMany(p => p.Inlines).OfType<HImage>()
            .Where(i => i.Anchor is not null).ToList();
        var seal = Assert.Single(floating);
        Assert.Equal(("PAPER", "PAPER", "IN_FRONT_OF_TEXT"), (seal.Anchor!.HorizontalRelativeTo, seal.Anchor.VerticalRelativeTo, seal.Anchor.Wrap));
        Assert.InRange(seal.Anchor.HorizontalOffset, 36900, 37100);
        Assert.InRange(seal.Anchor.VerticalOffset, 26900, 27100);
        Assert.InRange(seal.Width ?? 0, 9900, 10100);
        Assert.InRange(seal.Height ?? 0, 4900, 5100);
        using var bitmap = SKBitmap.Decode(seal.Path);
        var middle = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
        // Red over white paper: about 90 % opaque (colour to alpha against the page under it).
        Assert.True(middle.Red > 180 && middle.Green < 60 && middle.Alpha > 200, middle.ToString());
    }

    [Fact]
    public async Task Letter_becomes_a_valid_docx_with_its_tables_and_seal()
    {
        var path = Letter(fx.NewFolder());
        AddSeal(path, [370, 842 - 320, 470, 842 - 270]);

        var docx = await ConvertAsync(path, "docx");

        ValidPackage(docx);
        var tables = ReadBack(docx).Sections.SelectMany(s => s.Blocks).OfType<HTable>().ToList();
        Assert.Equal(2, tables.Count);
        if (await ConvertibleByLibreOfficeAsync(fx, docx) is { } pages)
            Assert.Equal(1, pages);
    }

    [Theory]
    [InlineData(255, 255, 255, 255, 255, 255, 0)]       // nothing drawn over white paper
    [InlineData(200, 30, 30, 255, 255, 255, 225)]       // a red seal over white: as opaque as its farthest channel
    [InlineData(200, 30, 30, 0, 0, 0, 200)]             // the seal over black text stays red, partly transparent
    public void Colour_to_alpha_against_the_page_under_the_annotation(byte r, byte g, byte b, byte ur, byte ug, byte ub, int alpha)
    {
        var color = PdfDocumentReader.ColorToAlpha(new SKColor(r, g, b), new SKColor(ur, ug, ub));

        Assert.InRange(color.Alpha, Math.Max(0, alpha - 2), Math.Min(255, alpha + 2));
        if (alpha > 0)
            Assert.True(color.Red > color.Green && color.Red > color.Blue, color.ToString());
    }
}
