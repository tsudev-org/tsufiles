// The annotation layer of a PDF page (signature fields, stamps, filled form fields) as pictures with transparency,
// for PdfDocumentReader.
//
// Vietnamese administrative documents signed with a digital certificate (VGCA and similar signing tools) carry the
// signature picture, the red seal, the document number and the day of signing as Widget annotations with their own
// appearance streams: they are not part of the page's content, so the words and pictures of the page do not include
// them. PDFium draws them; the page is rendered with and without its annotations, and the pixels that differ inside
// each annotation's rectangle become a PNG whose alpha is the difference ("colour to alpha" against the page under
// it). The picture floats in front of the text at the annotation's place on the paper, as the PDF shows it.

using PDFtoImage;
using SkiaSharp;
using UglyToad.PdfPig.Annotations;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

#pragma warning disable CA1416

namespace TsuFiles.Engines.Pdf;

internal sealed partial class PdfDocumentReader
{
    /// <summary>Annotations with appearance streams per page, at most: more are a form, not a signature.</summary>
    private const int MaxOverlays = 40;

    /// <summary>A picture floating in front of the page's text at a place on the paper (points, y up from the bottom).</summary>
    private sealed record Overlay(string Path, PdfRectangle Bounds);

    /// <summary>
    /// The visible annotations of <paramref name="page"/> that draw something (signatures, stamps, seals, filled form
    /// fields), as pictures with transparency. Links and pop-ups are left out (links are read with the words).
    /// </summary>
    private List<Overlay> AnnotationOverlays(Page page, PageContent content)
    {
        List<PdfRectangle> boxes;
        try
        {
            boxes = [.. page.GetAnnotations()
                .Where(a => a.HasNormalAppearance && a.Type is not (AnnotationType.Link or AnnotationType.Popup)
                            && (a.Flags & (AnnotationFlags.Hidden | AnnotationFlags.NoView | AnnotationFlags.Invisible)) == 0)
                .Select(a => Clip(a.Rectangle, content))
                .Where(b => b.Width >= 3 && b.Height >= 3)
                .Take(MaxOverlays)];
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NullReferenceException or InvalidCastException)
        {
            return []; // a broken annotation dictionary: the page reads as before
        }
        if (boxes.Count == 0)
            return [];

        var overlays = new List<Overlay>();
        using var with = Render(content.Index);
        using var without = Render(content.Index, annotations: false);
        if (with.Width != without.Width || with.Height != without.Height)
            return [];
        foreach (var box in boxes)
        {
            if (Difference(with, without, box, content) is { } path)
                overlays.Add(new Overlay(path, box));
        }
        return overlays;
    }

    private static PdfRectangle Clip(PdfRectangle box, PageContent content) =>
        new(Math.Max(0, box.Left), Math.Max(0, box.Bottom), Math.Min(content.Width, box.Right), Math.Min(content.Height, box.Top));

    /// <summary>
    /// The pixels of <paramref name="with"/> inside <paramref name="box"/> that differ from <paramref name="without"/>,
    /// as a PNG with alpha; null when the annotation draws nothing visible (an empty signature field).
    /// </summary>
    private string? Difference(SKBitmap with, SKBitmap without, PdfRectangle box, PageContent content)
    {
        var scaleX = with.Width / content.Width;
        var scaleY = with.Height / content.Height;
        var rect = SKRectI.Round(new SKRect(
            (float)(box.Left * scaleX), (float)((content.Height - box.Top) * scaleY),
            (float)(box.Right * scaleX), (float)((content.Height - box.Bottom) * scaleY)));
        rect.Intersect(new SKRectI(0, 0, with.Width, with.Height));
        if (rect.Width < 2 || rect.Height < 2)
            return null;

        using var layer = new SKBitmap(new SKImageInfo(rect.Width, rect.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var visible = 0;
        for (var y = 0; y < rect.Height; y++)
        {
            for (var x = 0; x < rect.Width; x++)
            {
                var shown = with.GetPixel(rect.Left + x, rect.Top + y);
                var under = without.GetPixel(rect.Left + x, rect.Top + y);
                var color = ColorToAlpha(shown, under);
                if (color.Alpha > 0)
                    visible++;
                layer.SetPixel(x, y, color);
            }
        }
        // A few stray pixels (anti-aliasing of an empty field's frame) are no picture.
        if (visible < Math.Max(4, rect.Width * rect.Height / 2000))
            return null;

        var path = NewMediaPath("png");
        using var image = SKImage.FromBitmap(layer);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    /// <summary>
    /// The colour and alpha that, drawn over <paramref name="under"/>, give <paramref name="shown"/> ("colour to alpha"
    /// against the pixel under it): white stays transparent over white paper, a red seal over black text stays red.
    /// </summary>
    internal static SKColor ColorToAlpha(SKColor shown, SKColor under)
    {
        static double Channel(byte a, byte b) => a > b ? (a - b) / (255.0 - b) : a < b ? (b - a) / (double)b : 0;
        var alpha = Math.Max(Channel(shown.Red, under.Red), Math.Max(Channel(shown.Green, under.Green), Channel(shown.Blue, under.Blue)));
        if (alpha < 0.03)
            return SKColors.Transparent;
        static byte Value(byte a, byte b, double alpha) => (byte)Math.Clamp(Math.Round(b + (a - b) / alpha), 0, 255);
        return new SKColor(Value(shown.Red, under.Red, alpha), Value(shown.Green, under.Green, alpha), Value(shown.Blue, under.Blue, alpha),
            (byte)Math.Round(alpha * 255));
    }

    private SKBitmap Render(int pageIndex, bool annotations)
    {
        _pdfBytes ??= File.ReadAllBytes(_path);
        return Conversion.ToImage(_pdfBytes, pageIndex, null,
            new RenderOptions(Dpi: RenderDpi, WithAnnotations: annotations, WithFormFill: annotations, BackgroundColor: SKColors.White));
    }
}
