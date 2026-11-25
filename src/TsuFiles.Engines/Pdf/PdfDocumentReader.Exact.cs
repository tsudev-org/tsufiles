// The exact layout of PDF → DOCX: every page of the Word file looks like the PDF's page.
//
// The flowing layout (ReadLayout) turns the words into paragraphs that Word lays out again: easy to edit, but lines,
// boxes, fills and drawings are lost and the text moves (other line breaks, other page breaks). The exact layout
// keeps the page as it is drawn:
//
//  * Behind the text, the page without its text: PDFium renders a copy of the PDF whose text is drawn invisibly
//    (PdfHiddenText), so every line, table rule, shape, fill and picture stays exactly where it is, at 200 dpi.
//  * The annotation layer (digital signatures, seals, the stamped number of a signed document, filled form fields)
//    floats in front of the text as pictures with transparency, as in the flowing layout (AnnotationOverlays).
//  * In front of it, the words as text in text boxes at their place on the paper: one box per run of words on a line
//    (a wide gap starts a new box), with the PDF's font, size, colour, bold and italic. Each word is fitted to its
//    width in the PDF with letter spacing and each gap to its width with the spacing of the space before the word,
//    measured with the font Word will use (SkiaSharp), so the words line up with the drawing behind them. The
//    baseline is placed from the font's ascent.
//  * Text that cannot be written back as it looks stays in the picture: words that do not run left to right (rotated
//    text), right-to-left words, and glyphs without a real character (a font without a Unicode map).
//
// Scanned pages are the rendered page (their invisible OCR text is not shown). Running headers, footers and page
// numbers are kept: they are part of the page. If the PDF cannot be rewritten (encrypted, broken), the flowing layout
// is used instead.

using System.Globalization;
using System.Text;
using PDFtoImage;
using SkiaSharp;
using TsuFiles.Engines.Hwp.Hwpx;
using TsuFiles.Engines.Infrastructure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Exceptions;

#pragma warning disable CA1416

namespace TsuFiles.Engines.Pdf;

internal sealed partial class PdfDocumentReader
{
    /// <summary>Resolution of the picture behind the text: thin rules stay sharp in print.</summary>
    private const int ExactDpi = 200;

    /// <summary>Above this size a PNG background becomes a JPEG (a photo or a coloured page).</summary>
    private const int MaxPngBytes = 1_500_000;

    /// <summary>The font used for words whose PDF font names no family (F1, T3Font_0).</summary>
    private const string FallbackFont = "Times New Roman";

    /// <summary>
    /// Reads <paramref name="path"/> for a DOCX that looks like the PDF (see the top of this file); falls back to
    /// <see cref="ReadLayout"/> if the PDF cannot be rewritten.
    /// </summary>
    public static HDocument ReadExact(string path, string mediaFolder) =>
        new PdfDocumentReader(path, mediaFolder, textOnly: false, layout: true).ReadExactDocument();

    private sealed record ExactLine(List<TextWord> Words)
    {
        public double Left => Words[0].Left;
        public double Right => Words[^1].Right;
        public double Baseline => Words[0].Baseline;
    }

    private HDocument ReadExactDocument()
    {
        _pdfBytes ??= File.ReadAllBytes(_path);
        if (PdfHiddenText.Create(_pdfBytes) is not { } withoutText)
            return ReadDocument();

        PdfDocument pdf;
        try
        {
            pdf = PdfDocument.Open(_path, new ParsingOptions { UseLenientParsing = true, SkipMissingFonts = true });
        }
        catch (PdfDocumentEncryptedException)
        {
            throw PasswordProtected();
        }

        var document = new HDocument { Title = null };
        var fonts = new ExactFonts();
        using (pdf)
        {
            HSection? section = null;
            try
            {
                foreach (var page in pdf.GetPages())
                {
                    var index = page.Number - 1;
                    var raster = new List<PdfRectangle>();
                    var words = PageWords(page, raster);
                    words.RemoveAll(w =>
                    {
                        if (!KeepsAsText(w))
                            raster.Add(new PdfRectangle(w.Left, w.Bottom, w.Right, w.Top));
                        return !KeepsAsText(w);
                    });

                    var hpage = new HPage(Hwp(page.Width), Hwp(page.Height), 0, 0, 0, 0, 0, 0, 0);
                    var newSection = section is null || section.Page != hpage;
                    if (newSection)
                    {
                        section = new HSection { Page = hpage };
                        document.Sections.Add(section);
                    }

                    // One tiny paragraph per page holds the page's picture and text boxes, all placed on the paper.
                    var holder = new HParagraph { MarkFormat = new HCharFormat(Size: 100), PageBreakBefore = !newSection };
                    if (Background(index, page.Width, page.Height, withoutText, raster, words.Count == 0) is { } picture)
                    {
                        holder.Inlines.Add(new HImage(picture)
                        {
                            Width = hpage.Width,
                            Height = hpage.Height,
                            Anchor = new HAnchor("PAPER", "LEFT", 0, "PAPER", "TOP", 0, "BEHIND_TEXT", AllowOverlap: true),
                        });
                    }
                    foreach (var line in ExactLines(words))
                        holder.Inlines.Add(LineBox(line, page.Height, fonts));
                    // The annotation layer (signatures, seals, the stamped number) in front, as the PDF shows it.
                    if (words.Count > 0)
                    {
                        foreach (var overlay in AnnotationOverlays(page, new PageContent(index, page.Width, page.Height)))
                        {
                            holder.Inlines.Add(new HImage(overlay.Path)
                            {
                                Width = Hwp(overlay.Bounds.Width),
                                Height = Hwp(overlay.Bounds.Height),
                                Anchor = new HAnchor("PAPER", "LEFT", Hwp(overlay.Bounds.Left), "PAPER", "TOP", Hwp(page.Height - overlay.Bounds.Top),
                                    "IN_FRONT_OF_TEXT", AllowOverlap: true),
                            });
                        }
                    }
                    section!.Blocks.Add(holder);
                }
            }
            catch (PdfDocumentEncryptedException)
            {
                throw PasswordProtected();
            }
        }
        if (document.Sections.Count == 0)
        {
            var empty = new HSection();
            empty.Blocks.Add(new HParagraph());
            document.Sections.Add(empty);
        }
        return document;
    }

    /// <summary>
    /// False for a word that would not look the same as text: right-to-left text (its order on the page is not the
    /// order Word lays out) and glyphs without a real character (private use, replacement characters).
    /// </summary>
    private static bool KeepsAsText(TextWord word) =>
        !word.DrawnForward
        && ComplexScripts.Direction(word.Text) >= 0
        && !word.Text.Any(c => c is '�' || (c >= '' && c <= '') || char.IsControl(c));

    // ───────────────────────── The picture behind the text ─────────────────────────

    /// <summary>
    /// The page rendered without its text, with the text of <paramref name="raster"/> (kept as pixels) copied in from
    /// the page as it is; null for a page with nothing but its text on white paper.
    /// </summary>
    private string? Background(int index, double width, double height, byte[] withoutText, List<PdfRectangle> raster, bool wholePage)
    {
        // A page without words is the page as shown, annotations included; otherwise the annotations are pictures of
        // their own in front of the text (AnnotationOverlays).
        using var bitmap = wholePage
            ? RenderAt(_pdfBytes!, index, ExactDpi, annotations: true)
            : RenderAt(withoutText, index, ExactDpi, annotations: false);
        if (!wholePage && raster.Count > 0)
        {
            using var shown = RenderAt(_pdfBytes!, index, ExactDpi, annotations: false);
            if (shown.Width == bitmap.Width && shown.Height == bitmap.Height)
            {
                using var canvas = new SKCanvas(bitmap);
                var scaleX = bitmap.Width / width;
                var scaleY = bitmap.Height / height;
                foreach (var box in raster)
                {
                    // A little room around the glyph boxes: accents and antialiasing reach beyond them.
                    var pad = Math.Max(1.5, 0.15 * Math.Min(box.Width, box.Height));
                    var rect = SKRect.Create(
                        (float)((box.Left - pad) * scaleX), (float)((height - box.Top - pad) * scaleY),
                        (float)((box.Width + 2 * pad) * scaleX), (float)((box.Height + 2 * pad) * scaleY));
                    canvas.DrawBitmap(shown, rect, rect);
                }
            }
        }
        if (IsWhite(bitmap))
            return null;

        using var image = SKImage.FromBitmap(bitmap);
        var png = image.Encode(SKEncodedImageFormat.Png, 100);
        if (png.Size <= MaxPngBytes)
        {
            var path = NewMediaPath("png");
            using (png)
                File.WriteAllBytes(path, png.ToArray());
            return path;
        }
        png.Dispose();
        var jpegPath = NewMediaPath("jpg");
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        File.WriteAllBytes(jpegPath, jpeg.ToArray());
        return jpegPath;
    }

    private static SKBitmap RenderAt(byte[] pdf, int index, int dpi, bool annotations) =>
        Conversion.ToImage(pdf, index, null,
            new RenderOptions(Dpi: dpi, WithAnnotations: annotations, WithFormFill: annotations, BackgroundColor: SKColors.White));

    /// <summary>True if no pixel is darker than paper white (nothing to show behind the text).</summary>
    private static bool IsWhite(SKBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y += 2)
        {
            for (var x = 0; x < bitmap.Width; x += 2)
            {
                var c = bitmap.GetPixel(x, y);
                if (c.Red < 248 || c.Green < 248 || c.Blue < 248)
                    return false;
            }
        }
        return true;
    }

    // ───────────────────────── The words in front ─────────────────────────

    /// <summary>
    /// The words as runs on a line: words on the same baseline, left to right, split where the gap is wider than two
    /// ems (columns, table cells, a date far to the right), so each run sits where the PDF has it.
    /// </summary>
    private static List<ExactLine> ExactLines(List<TextWord> words)
    {
        var lines = new List<ExactLine>();
        var rows = new List<List<TextWord>>();
        foreach (var word in words.OrderByDescending(w => w.Baseline).ThenBy(w => w.Left))
        {
            var row = rows.FirstOrDefault(r => Math.Abs(r[0].Baseline - word.Baseline) <= 0.2 * Math.Min(r[0].Size, word.Size)
                                               && Math.Abs(r[0].Size - word.Size) <= 0.35 * Math.Max(r[0].Size, word.Size));
            if (row is null)
                rows.Add([word]);
            else
                row.Add(word);
        }
        foreach (var row in rows)
        {
            row.Sort((a, b) => a.Left.CompareTo(b.Left));
            var current = new List<TextWord> { row[0] };
            for (var i = 1; i < row.Count; i++)
            {
                var gap = row[i].Left - current[^1].Right;
                if (gap > 2 * Math.Max(row[i].Size, current[^1].Size) || gap < -0.5 * row[i].Size)
                {
                    lines.Add(new ExactLine(current));
                    current = [];
                }
                current.Add(row[i]);
            }
            lines.Add(new ExactLine(current));
        }
        return lines;
    }

    /// <summary>A text box without border or fill holding <paramref name="line"/> at its place on the paper.</summary>
    private static HTextBox LineBox(ExactLine line, double pageHeight, ExactFonts fonts)
    {
        var paragraph = new HParagraph
        {
            Format = new HParaFormat(Align: HAlign.Left, Left: 0, Right: 0, FirstLine: 0, Before: 0, After: 0,
                LineSpacing: new HLineSpacing(HLineSpacingKind.Percent, 130), WidowOrphan: false),
        };

        // Positions in twips from the box's left edge, as Word will lay them out (rounded like Word's w:spacing).
        var x = 0;
        var ascent = 0.0;
        var descent = 0.0;
        HInline? Run(string text, HCharFormat format, string? link)
        {
            var run = new HText(text, format);
            if (link is null)
                return run;
            var hyperlink = new HLink(link);
            hyperlink.Content.Add(run);
            return hyperlink;
        }

        for (var i = 0; i < line.Words.Count; i++)
        {
            var word = line.Words[i];
            var format = word.Format with { Font = word.Format.Font ?? FallbackFont, Underline = null };
            var sizePt = (format.Size ?? 1000) / 100.0;
            var measure = fonts.Get(format.Font!, format.Bold == true, format.Italic == true);
            var metrics = measure?.Metrics(sizePt) ?? (0.93 * sizePt, 0.216 * sizePt);
            ascent = Math.Max(ascent, metrics.Ascent);
            descent = Math.Max(descent, metrics.Descent);
            var text = word.Text.Normalize(NormalizationForm.FormC);

            if (i > 0)
            {
                var target = (int)Math.Round((word.Left - line.Left) * 20);
                var spaceFormat = format with { Bold = null, Italic = null };
                if (measure is not null)
                {
                    var space = (int)Math.Round(measure.Width(" ", sizePt) * 20);
                    var tracking = Math.Clamp(target - x - space, -space + 1, 31680);
                    paragraph.Inlines.Add(new HText(" ", spaceFormat with { Tracking = tracking * 5 }));
                    x += space + tracking;
                }
                else
                {
                    paragraph.Inlines.Add(new HText(" ", spaceFormat));
                }
            }

            if (measure is not null)
            {
                var natural = measure.Width(text, sizePt) * 20;
                var count = Math.Max(1, new StringInfo(text).LengthInTextElements);
                var wanted = (word.Right - word.Left) * 20;
                var perLetter = (int)Math.Round(Math.Clamp((wanted - natural) / count, -0.4 * sizePt * 20, 0.6 * sizePt * 20));
                paragraph.Inlines.Add(Run(text, perLetter == 0 ? format : format with { Tracking = perLetter * 5 }, word.Link)!);
                x += (int)Math.Round(natural) + perLetter * count;
            }
            else
            {
                paragraph.Inlines.Add(Run(text, format, word.Link)!);
            }
        }

        var size = line.Words.Max(w => w.Size);
        var width = Math.Max(line.Right - line.Left, x / 20.0) + 0.6 * size + 2;
        var top = pageHeight - line.Baseline - ascent;
        var box = new HTextBox
        {
            Width = Hwp(width),
            Height = Hwp((ascent + descent) * 1.15 + 1),
            Anchor = new HAnchor("PAPER", "LEFT", Hwp(line.Left), "PAPER", "TOP", Hwp(top), "IN_FRONT_OF_TEXT", AllowOverlap: true),
            Line = HBorder.None,
            Padding = new HInsets(0, 0, 0, 0),
            NoWrap = true,
        };
        box.Blocks.Add(paragraph);
        return box;
    }

    /// <summary>The fonts Word will use, measured with SkiaSharp; null where the font is not installed here.</summary>
    private sealed class ExactFonts
    {
        private readonly Dictionary<(string, bool, bool), FontMeasure?> _fonts = [];

        public FontMeasure? Get(string family, bool bold, bool italic)
        {
            if (_fonts.TryGetValue((family, bold, italic), out var known))
                return known;
            var style = new SKFontStyle(bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal,
                italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
            var typeface = SKTypeface.FromFamilyName(family, style);
            FontMeasure? measure = null;
            if (typeface is not null && string.Equals(typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase))
                measure = new FontMeasure(typeface);
            else
                typeface?.Dispose();
            _fonts[(family, bold, italic)] = measure;
            return measure;
        }
    }

    private sealed class FontMeasure(SKTypeface typeface)
    {
        private readonly SKFont _font = new(typeface, 100) { Subpixel = true, LinearMetrics = true };

        /// <summary>The advance width of <paramref name="text"/> in points at <paramref name="size"/> points.</summary>
        public double Width(string text, double size) => _font.MeasureText(text) * size / 100;

        /// <summary>
        /// From the top of a single-spaced line to its baseline (Word puts the font's line gap above the ascent), and
        /// the descent, in points at <paramref name="size"/> points.
        /// </summary>
        public (double Ascent, double Descent) Metrics(double size)
        {
            var metrics = _font.Metrics;
            return ((Math.Max(0, metrics.Leading) - metrics.Ascent) * size / 100, metrics.Descent * size / 100);
        }
    }
}
