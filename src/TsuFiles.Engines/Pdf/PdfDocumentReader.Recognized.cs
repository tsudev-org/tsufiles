// The exact layout of a scanned page whose text was recognized (PdfOptions.Ocr): the recognized words become text in
// front of the scan, so the scan must stop showing them. For each word, the paper around it gives the colour to paint
// and the strokes inside its box give the colour of its text; only strokes of that colour (or neutral dark ones) are
// painted over, so a red stamp or a blue signature across the word stays. A word whose strokes are clearly thicker,
// for its height, than the page's usual ones is bold (the headings and the "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM" lines
// of administrative documents).

using SkiaSharp;

namespace TsuFiles.Engines.Pdf;

internal sealed partial class PdfDocumentReader
{
    private static class RecognizedWords
    {
        /// <summary>
        /// Stroke width, in ems, from which a line is bold. Measured on scanned decrees (less the pixel of blur): body
        /// text 0.085-0.12 em, bold headings 0.143-0.16 em.
        /// </summary>
        private const double BoldStroke = 0.132;

        /// <summary>
        /// The size and weight of each line of recognized words (Tesseract only places them). The size comes from the
        /// words' widths measured in the font they will be written in (their height includes accents and descenders,
        /// so it overstates capitals and understates lower case), the median of the line's words, to half a point. A
        /// line is bold when its strokes are as thick as bold print for that size; it is then sized in the bold face.
        /// </summary>
        public static void Size(List<TextWord> words, Dictionary<int, (double Stroke, double Slant)> strokes, Func<bool, FontMeasure?> font)
        {
            var regular = font(false);
            var heavy = font(true);
            if (regular is null)
                return;
            var rows = new List<List<int>>();
            foreach (var i in Enumerable.Range(0, words.Count).Where(i => words[i].Recognized).OrderByDescending(i => words[i].Baseline))
            {
                var row = rows.FirstOrDefault(l => Math.Abs(words[l[0]].Baseline - words[i].Baseline) <= 0.3 * words[l[0]].Size);
                if (row is null)
                    rows.Add([i]);
                else
                    row.Add(i);
            }
            // A row splits where the gap is wide (two columns: "Số: 154/2025/NĐ-CP" and the italic date beside it).
            var lines = new List<List<int>>();
            foreach (var row in rows)
            {
                row.Sort((a, b) => words[a].Left.CompareTo(words[b].Left));
                var current = new List<int> { row[0] };
                foreach (var i in row.Skip(1))
                {
                    if (words[i].Left - words[current[^1]].Right > 2.5 * Math.Max(words[i].Size, words[current[^1]].Size))
                    {
                        lines.Add(current);
                        current = [];
                    }
                    current.Add(i);
                }
                lines.Add(current);
            }

            // Italic leans relative to the page's own print: a scan turned a little leans every line alike.
            static IEnumerable<int> Leaning(List<int> line, List<TextWord> words, Dictionary<int, (double Stroke, double Slant)> strokes) =>
                line.Where(i => strokes.ContainsKey(i) && !words[i].Text.Any(c => c is '/' or '\\' or '-' or '–' or '—'));
            var pageSlants = lines.SelectMany(l => Leaning(l, words, strokes)).Select(i => strokes[i].Slant).Order().ToList();
            var upright = pageSlants.Count == 0 ? 1 : pageSlants[pageSlants.Count / 2];

            foreach (var line in lines)
            {
                double? Measured(FontMeasure measure)
                {
                    var sizes = line.Where(i => words[i].Text.Length >= 2)
                        .Select(i => (words[i].Right - words[i].Left) / Math.Max(0.01, measure.Width(words[i].Text, 1)))
                        .Where(v => v > 0).Order().ToList();
                    return sizes.Count == 0 ? null : sizes[sizes.Count / 2];
                }
                var tesseract = line.Select(i => words[i].Size).Order().ElementAt(line.Count / 2);
                var size = Measured(regular) ?? tesseract;
                var stroke = line.Where(strokes.ContainsKey).Select(i => strokes[i].Stroke).Order().ToList();
                // Slashes and dashes lean by themselves ("2324/UBND-VP"): those words don't count, and one word is too few.
                var slants = Leaning(line, words, strokes).Select(i => strokes[i].Slant).Order().ToList();
                var italic = slants.Count >= 2 && slants[slants.Count / 2] - upright >= ItalicLean;
                var bold = stroke.Count > 0 && size > 0 && stroke[stroke.Count / 2] / size >= BoldStroke;
                if (bold && heavy is not null)
                    size = Measured(heavy) ?? size;
                size = Math.Round(Math.Clamp(size, 0.6 * tesseract, 1.4 * tesseract) * 2) / 2;
                foreach (var i in line)
                {
                    var word = words[i];
                    words[i] = word with
                    {
                        Size = size,
                        Format = word.Format with { Size = (int)Math.Round(size * 100), Bold = bold ? true : null, Italic = italic ? true : null },
                    };
                    words[i] = Corrected(words[i], line.Select(j => words[j]).ToList());
                }
            }
        }

        /// <summary>
        /// Paints the strokes of the recognized words in <paramref name="words"/> out of <paramref name="bitmap"/> (the
        /// page at <paramref name="width"/> × <paramref name="height"/> points) and gives each its colour and weight.
        /// </summary>
        public static Dictionary<int, (double Stroke, double Slant)> Clean(SKBitmap bitmap, List<TextWord> words, double width, double height)
        {
            var strokes = new Dictionary<int, (double Stroke, double Slant)>();
            var pixels = bitmap.Pixels;
            var w = bitmap.Width;
            var h = bitmap.Height;
            var scaleX = w / width;
            var scaleY = h / height;
            var found = new List<(int Index, SKColor Colour, double Stroke, double Slant)>();
            var erase = new List<(int X0, int Y0, int X1, int Y1, SKColor Paper, SKColor Ink, int Threshold)>();

            for (var i = 0; i < words.Count; i++)
            {
                var word = words[i];
                if (!word.Recognized)
                    continue;
                var x0 = Math.Clamp((int)Math.Floor(word.Left * scaleX) - 1, 0, w - 1);
                var x1 = Math.Clamp((int)Math.Ceiling(word.Right * scaleX) + 1, 0, w - 1);
                var y0 = Math.Clamp((int)Math.Floor((height - word.Top) * scaleY) - 1, 0, h - 1);
                var y1 = Math.Clamp((int)Math.Ceiling((height - word.Bottom) * scaleY) + 1, 0, h - 1);
                if (x1 <= x0 || y1 <= y0)
                    continue;

                // The paper: the light part of the box and a margin around it.
                var pad = Math.Max(3, (y1 - y0) / 3);
                var around = new List<int>();
                for (var y = Math.Max(0, y0 - pad); y <= Math.Min(h - 1, y1 + pad); y += 1)
                {
                    for (var x = Math.Max(0, x0 - pad); x <= Math.Min(w - 1, x1 + pad); x += 2)
                        around.Add(Luma(pixels[y * w + x]));
                }
                around.Sort();
                var paperLuma = around[(int)(around.Count * 0.85)];
                var threshold = paperLuma - Math.Max(35, (int)(0.3 * paperLuma));
                long pr = 0, pg = 0, pb = 0, pn = 0;
                for (var y = Math.Max(0, y0 - pad); y <= Math.Min(h - 1, y1 + pad); y += 1)
                {
                    for (var x = Math.Max(0, x0 - pad); x <= Math.Min(w - 1, x1 + pad); x += 2)
                    {
                        var c = pixels[y * w + x];
                        if (Luma(c) >= paperLuma - 8)
                        {
                            pr += c.Red;
                            pg += c.Green;
                            pb += c.Blue;
                            pn++;
                        }
                    }
                }
                var paper = pn == 0 ? SKColors.White : new SKColor((byte)(pr / pn), (byte)(pg / pn), (byte)(pb / pn));

                // The strokes: their darker half gives the colour, their horizontal runs the stroke width.
                var ink = new List<SKColor>();
                var spots = new List<(int X, int Y)>();
                var runs = 0;
                for (var y = y0; y <= y1; y++)
                {
                    var inRun = false;
                    for (var x = x0; x <= x1; x++)
                    {
                        var c = pixels[y * w + x];
                        var dark = Luma(c) < threshold;
                        if (dark)
                        {
                            ink.Add(c);
                            spots.Add((x - x0, y1 - y));
                            if (!inRun)
                                runs++;
                        }
                        inRun = dark;
                    }
                }
                if (ink.Count < 3)
                    continue;
                ink.Sort((a, b) => Luma(a).CompareTo(Luma(b)));
                var darker = ink.Take(Math.Max(1, ink.Count / 2)).ToList();
                var colour = new SKColor((byte)darker.Average(c => c.Red), (byte)darker.Average(c => c.Green), (byte)darker.Average(c => c.Blue));
                // A scan greys black print: dark neutral strokes are black text.
                if (Chroma(colour) < 50)
                    colour = SKColors.Black;
                // Stroke width in points: the average horizontal run of ink, less the pixel a scan's blur adds.
                found.Add((i, colour, Math.Max(0, (double)ink.Count / Math.Max(1, runs) - 1) / scaleX, Slant(spots, x1 - x0 + 1, y1 - y0 + 1)));
                erase.Add((x0, y0, x1, y1, paper, colour, threshold + 20));
            }
            if (found.Count == 0)
                return strokes;

            // Paint the strokes out (after every word was measured: neighbouring boxes overlap).
            foreach (var (x0, y0, x1, y1, paper, colour, threshold) in erase)
            {
                for (var y = Math.Max(0, y0 - 1); y <= Math.Min(h - 1, y1 + 1); y++)
                {
                    for (var x = Math.Max(0, x0 - 1); x <= Math.Min(w - 1, x1 + 1); x++)
                    {
                        var c = pixels[y * w + x];
                        if (Luma(c) < threshold && (Chroma(c) < 60 || Distance(c, colour) < 110))
                            pixels[y * w + x] = paper;
                    }
                }
            }
            bitmap.Pixels = pixels;

            foreach (var (index, colour, stroke, slant) in found)
            {
                var word = words[index];
                words[index] = word with
                {
                    Format = word.Format with { Color = colour == SKColors.Black ? null : $"#{colour.Red:X2}{colour.Green:X2}{colour.Blue:X2}" },
                };
                strokes[index] = (stroke, slant);
            }
            return strokes;
        }

        /// <summary>
        /// How much sharper, beyond the page's usual print, a line's strokes are when leaned back like italic print:
        /// from this on it is italic. Measured on scanned decrees: upright lines 0.82-0.92, italic ones 1.07-1.21.
        /// </summary>
        private const double ItalicLean = 0.12;

        /// <summary>The OCR fix of <see cref="ScanText.FixDigits"/> applied to a word of its line.</summary>
        private static TextWord Corrected(TextWord word, List<TextWord> line)
        {
            var before = line.Where(w => w.Right <= word.Left).OrderBy(w => w.Right).LastOrDefault()?.Text;
            var text = ScanText.FixDigits(word.Text, before);
            return text == word.Text ? word : word with { Text = text };
        }

        private static double Slant(List<(int X, int Y)> spots, int width, int height) => ScanText.Slant(spots, width, height);

        private static int Luma(SKColor c) => (299 * c.Red + 587 * c.Green + 114 * c.Blue) / 1000;

        private static int Chroma(SKColor c) => Math.Max(c.Red, Math.Max(c.Green, c.Blue)) - Math.Min(c.Red, Math.Min(c.Green, c.Blue));

        private static double Distance(SKColor a, SKColor b) =>
            Math.Sqrt(Math.Pow(a.Red - b.Red, 2) + Math.Pow(a.Green - b.Green, 2) + Math.Pow(a.Blue - b.Blue, 2));
    }
}

/// <summary>Heuristics for the words recognized on a scan (PdfDocumentReader.RecognizedWords).</summary>
internal static class ScanText
{
    /// <summary>
    /// Italic print leans about 12° (a shear of 0.21): undone, its vertical strokes stack into sharp columns. The ratio
    /// of the column profile's energy with that shear undone to without it, for ink at <paramref name="spots"/> (x from
    /// the left, y up from the bottom of a <paramref name="width"/> × <paramref name="height"/> box). 1 for too little ink.
    /// </summary>
    public static double Slant(IReadOnlyList<(int X, int Y)> spots, int width, int height)
    {
        if (spots.Count < 20)
            return 1;
        double Energy(double shear)
        {
            var columns = new int[width + (int)Math.Ceiling(shear * height) + 2];
            foreach (var (x, y) in spots)
                columns[Math.Clamp((int)Math.Round(x + shear * (height - y)), 0, columns.Length - 1)]++;
            return columns.Sum(c => (double)c * c);
        }
        var upright = Energy(0);
        return upright == 0 ? 1 : Energy(0.21) / upright;
    }

    /// <summary>
    /// Recognition's commonest slip in these documents: "1" read as "I", "l" or "|" in numbers ("I1", "tháng Il"). A
    /// word of digits and those letters only becomes digits when it has a digit or follows "ngày", "tháng", "năm" or
    /// "Số" (<paramref name="previous"/>, the word before it on its line).
    /// </summary>
    public static string FixDigits(string text, string? previous)
    {
        if (text.Length is 0 or > 6 || !text.All(c => char.IsAsciiDigit(c) || c is 'I' or 'l' or '|') || text.All(char.IsAsciiDigit))
            return text;
        var before = previous?.TrimEnd(':').ToLowerInvariant();
        if (!text.Any(char.IsAsciiDigit) && before is not ("ngày" or "tháng" or "năm" or "số"))
            return text;
        return new string([.. text.Select(c => c is 'I' or 'l' or '|' ? '1' : c)]);
    }
}
