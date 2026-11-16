// Blocks of text side by side without rules, read as tables without borders, for PdfDocumentReader.
//
// Word documents lay out text next to other text with a table whose borders are hidden: the heading of every
// Vietnamese administrative document (the issuing body on the left, the national motto on the right, the document
// number next to the place and date), the closing block (recipients on the left, the signer on the right), forms with
// a label and its value. In the PDF nothing marks the table; read as paragraphs, the blocks come out one under the
// other and the page loses its shape. Here paragraphs that sit next to each other in a band of the page, with a clear
// gap between them, become a table without borders: one column per side, one row per band, adjacent bands with the
// same columns sharing a table. Real column layouts (newspapers, papers) are left to the reading order: their bands
// are much taller than such a block.

namespace TsuFiles.Engines.Pdf;

internal sealed partial class PdfDocumentReader
{
    /// <summary>A band taller than this share of the page is a column layout, not a block side by side.</summary>
    private const double MaxBandShare = 0.45;

    /// <summary>
    /// Tables without borders from the paragraphs of <paramref name="items"/> that sit side by side; the paragraphs taken
    /// are removed from <paramref name="items"/>. <paramref name="pictures"/> next to the text widen the bands (a QR code
    /// under the list of recipients) and go into the cells later (<see cref="PlacePictures"/>).
    /// </summary>
    private static List<RuledTable> LayoutTables(List<Item> items, List<Item> pictures, double pageHeight)
    {
        var text = items.Where(i => !i.IsPicture && !i.IsTable && i.Lines.Count > 0).ToList();
        if (text.Count < 2)
            return [];

        var bands = new List<Band>();
        var used = new HashSet<Item>();
        foreach (var seed in text.OrderByDescending(i => i.Top))
        {
            if (used.Contains(seed))
                continue;
            var members = new List<Item> { seed };
            var (top, bottom) = (seed.Top, seed.Bottom);
            for (var grown = true; grown;)
            {
                grown = false;
                foreach (var other in text)
                {
                    if (used.Contains(other) || members.Contains(other) || Overlap(other, top, bottom) < 0.3 * other.Size)
                        continue;
                    members.Add(other);
                    (top, bottom) = (Math.Max(top, other.Top), Math.Min(bottom, other.Bottom));
                    grown = true;
                }
            }
            if (members.Count < 2 || top - bottom > MaxBandShare * pageHeight)
                continue;
            var gaps = ColumnGaps(members);
            if (gaps.Count == 0)
                continue;
            // Every column holds text: a paragraph and a word far to its right on another line are no block.
            var columns = Columns(members, gaps);
            if (columns.Any(c => c.Count == 0))
                continue;
            // List numbers or bullets with their text ("1.", "a)", "•") are a list, not a block.
            if (columns[0].All(m => m.Lines.Count == 1 && m.Lines[0].Words.Count == 1 && ListStart.IsMatch(m.Lines[0].Words[0].Text)))
                continue;
            foreach (var member in members)
                used.Add(member);
            bands.Add(new Band(members, gaps, top, bottom));
        }
        if (bands.Count == 0)
            return [];

        // Bands right under each other with the same columns are the rows of one table.
        var others = text.Where(i => !used.Contains(i)).ToList();
        var groups = new List<List<Band>>();
        foreach (var band in bands.OrderByDescending(b => b.Top))
        {
            var last = groups.Count > 0 ? groups[^1] : null;
            var previous = last?[^1];
            var between = previous is not null && others.Any(o => o.Top < previous.Bottom && o.Bottom > band.Top);
            // A band split at more places than the one above ("Số:  …/CAT-ANM" and the place and date under the two
            // headings) keeps the gaps of the band above: its other gaps are blanks in a line.
            var close = previous is not null && !between && previous.Bottom - band.Top < 4 * band.Members.Max(m => m.Size);
            if (close && band.Gaps.Count > previous!.Gaps.Count && Reduced(previous.Gaps, band) is { } reduced)
                band.Gaps = reduced;
            if (close && previous!.Gaps.Count == band.Gaps.Count && Compatible(previous.Gaps, band.Gaps))
                last!.Add(band);
            else
                groups.Add([band]);
        }

        // A block goes on under the last band in one of its columns: the signer's name under the room left for the
        // signature, the subject line ("V/v …") under the document number.
        var (areaLeft, areaRight) = (text.Min(i => i.Left), text.Max(i => i.Right));
        foreach (var group in groups)
        {
            var last = group[^1];
            var gaps = Enumerable.Range(0, last.Gaps.Count)
                .Select(g => (Start: group.Max(b => b.Gaps[g].Start), End: group.Min(b => b.Gaps[g].End))).ToList();
            if (gaps.Any(g => g.End <= g.Start))
                continue;
            while (Below(text, used, last, gaps, areaLeft, areaRight) is { } next && group[0].Top - next.Bottom <= MaxBandShare * pageHeight)
            {
                last.Members.Add(next);
                last.Bottom = Math.Min(last.Bottom, next.Bottom);
                used.Add(next);
            }
            foreach (var band in group)
                band.Gaps = [.. gaps];
        }

        // Pictures beside the text of a band (fully in one column, overlapping it vertically) belong to it.
        foreach (var band in bands)
        {
            foreach (var picture in pictures)
            {
                var inColumn = !band.Gaps.Any(g => picture.Left < g.End && picture.Right > g.Start);
                var inside = picture.Left >= band.Members.Min(m => m.Left) - 2 && picture.Right <= band.Members.Max(m => m.Right) + 2;
                if (inColumn && inside && Overlap(picture, band.Top, band.Bottom) > 0.5 * (picture.Top - picture.Bottom))
                    (band.Top, band.Bottom) = (Math.Max(band.Top, picture.Top), Math.Min(band.Bottom, picture.Bottom));
            }
        }

        var tables = new List<RuledTable>();
        foreach (var group in groups)
        {
            // A form ("Name:  …", "Phone:  …"): a first column of labels only stays tabbed rows.
            if (group.All(b => Columns(b.Members, b.Gaps)[0] is var labels && labels.All(m => m.Lines.All(l => l.Text.TrimEnd().EndsWith(':')))))
                continue;
            var table = Table(group, areaLeft, areaRight);
            tables.Add(table);
            foreach (var member in group.SelectMany(b => b.Members))
                items.Remove(member);
        }
        return tables;
    }

    private sealed class Band(List<Item> members, List<(double Start, double End)> gaps, double top, double bottom)
    {
        public List<Item> Members { get; } = members;
        public List<(double Start, double End)> Gaps { get; set; } = gaps;
        public double Top { get; set; } = top;
        public double Bottom { get; set; } = bottom;
    }

    /// <summary>
    /// The paragraph right under <paramref name="band"/> (within 10 em of its bottom: the room for a signature; nothing
    /// beside it) that fits in one of its columns, or null. It may narrow the gaps beside its column (a longer line, a
    /// wider name), as long as they stay open: <paramref name="gaps"/> is updated.
    /// </summary>
    private static Item? Below(List<Item> text, HashSet<Item> used, Band band, List<(double Start, double End)> gaps, double areaLeft, double areaRight)
    {
        var next = text.Where(i => !used.Contains(i) && i.Top <= band.Bottom + 0.3 * i.Size).MaxBy(i => i.Top);
        if (next is null || band.Bottom - next.Top > 10 * next.Size || next.Left < areaLeft - 2 || next.Right > areaRight + 2)
            return null;
        // A paragraph with others beside it starts a band of its own ("Số:" next to the place and date).
        if (text.Any(o => o != next && !used.Contains(o) && Overlap(o, next.Top, next.Bottom) >= 0.3 * o.Size))
            return null;
        // The column it starts in; it may reach into the gaps on either side, as long as 6 points stay free.
        var column = gaps.Count(g => next.Left >= (g.Start + g.End) / 2);
        // Right under the band (2.5 em), or filling the room its column left free while the others went on (the
        // signer's name under the space for the signature, beside the list of recipients).
        var columnBottom = band.Members.Where(m => gaps.Count(g => m.Left >= (g.Start + g.End) / 2) == column).Select(m => m.Bottom).DefaultIfEmpty(band.Bottom).Min();
        if (band.Bottom - next.Top > 2.5 * next.Size && columnBottom - band.Bottom < 2 * next.Size)
            return null;
        var narrowed = gaps.ToList();
        if (column > 0 && next.Left < gaps[column - 1].End)
            narrowed[column - 1] = (gaps[column - 1].Start, next.Left);
        if (column < gaps.Count && next.Right > gaps[column].Start)
            narrowed[column] = (next.Right, gaps[column].End);
        if (narrowed.Any(g => g.End - g.Start < 6))
            return null;
        for (var g = 0; g < gaps.Count; g++)
            gaps[g] = narrowed[g];
        return next;
    }

    private static double Overlap(Item item, double top, double bottom) => Math.Min(item.Top, top) - Math.Max(item.Bottom, bottom);

    /// <summary>
    /// Horizontal strips no paragraph of the band crosses, wide enough to be a gap between blocks (1.5 em, at least
    /// 12 points), between the band's left and right edge.
    /// </summary>
    private static List<(double Start, double End)> ColumnGaps(List<Item> members)
    {
        var spans = members.Select(m => (m.Left, m.Right)).OrderBy(s => s.Left).ToList();
        var minimum = Math.Max(12, 1.5 * members.Max(m => m.Size));
        var gaps = new List<(double, double)>();
        var reach = spans[0].Right;
        foreach (var (left, right) in spans.Skip(1))
        {
            if (left - reach >= minimum)
                gaps.Add((reach, left));
            reach = Math.Max(reach, right);
        }
        return gaps;
    }

    private static List<List<Item>> Columns(List<Item> members, List<(double Start, double End)> gaps)
    {
        var columns = Enumerable.Range(0, gaps.Count + 1).Select(_ => new List<Item>()).ToList();
        foreach (var member in members)
            columns[gaps.Count(g => member.Left >= g.End)].Add(member);
        return columns;
    }

    /// <summary>
    /// The gaps of <paramref name="band"/> that match <paramref name="columns"/> one by one, when every column of it
    /// still holds text; null otherwise.
    /// </summary>
    private static List<(double Start, double End)>? Reduced(List<(double Start, double End)> columns, Band band)
    {
        var kept = new List<(double Start, double End)>();
        foreach (var column in columns)
        {
            var match = band.Gaps.Where(g => Math.Min(g.End, column.End) > Math.Max(g.Start, column.Start)).ToList();
            if (match.Count != 1)
                return null;
            kept.Add(match[0]);
        }
        return Columns(band.Members, kept).All(c => c.Count > 0) ? kept : null;
    }

    /// <summary>Two bands share columns when each gap of one overlaps the gap of the other at the same place.</summary>
    private static bool Compatible(List<(double Start, double End)> a, List<(double Start, double End)> b) =>
        a.Zip(b).All(p => Math.Min(p.First.End, p.Second.End) > Math.Max(p.First.Start, p.Second.Start));

    /// <summary>
    /// The borderless grid of a group of bands. The column edges lie in the middle of the shared gaps; the outer edges
    /// keep a column whose text is centred centred in its cell (the motto over its underline, the signer's title over
    /// the name) as far as the page's text area allows, and touch the text otherwise.
    /// </summary>
    private static RuledTable Table(List<Band> group, double areaLeft, double areaRight)
    {
        var members = group.SelectMany(b => b.Members).ToList();
        var gapCount = group[0].Gaps.Count;
        var gaps = Enumerable.Range(0, gapCount)
            .Select(g => (Start: group.Max(b => b.Gaps[g].Start), End: group.Min(b => b.Gaps[g].End)))
            .Select(g => g.Start < g.End ? g : (Start: g.End, End: g.Start))
            .ToList();
        var columns = Columns(members, gaps);
        // Word sets the text a little wider than the PDF measures it (2 to 5 %): each column gets that much room on its
        // right out of the gap, or the gap's middle when the gap is wider.
        var inner = gaps.Select((g, i) =>
        {
            var width = columns[i].Count == 0 ? 0 : columns[i].Max(m => m.Right) - columns[i].Min(m => m.Left);
            return Math.Max((g.Start + g.End) / 2, Math.Min(g.End - 2, g.Start + 0.06 * width));
        }).ToList();

        var (left, right) = (members.Min(m => m.Left), members.Max(m => m.Right));
        var first = columns[0];
        if (first.Count > 0 && Centred(first))
        {
            var center = (first.Min(m => m.Left) + first.Max(m => m.Right)) / 2;
            left = Math.Min(left, 2 * center - inner[0]);
        }
        var lastColumn = columns[^1];
        if (lastColumn.Count > 0 && Centred(lastColumn))
        {
            var center = (lastColumn.Min(m => m.Left) + lastColumn.Max(m => m.Right)) / 2;
            right = Math.Max(right, 2 * center - inner[^1]);
        }
        // Not past the text of the page: the table must not widen the page's text area (its margins).
        double[] edges = [Math.Max(left, areaLeft), .. inner, Math.Min(right, areaRight)];

        var rows = new List<double> { group[0].Top + 1 };
        for (var b = 1; b < group.Count; b++)
            rows.Add((group[b - 1].Bottom + group[b].Top) / 2);
        rows.Add(group[^1].Bottom - 1);

        var cells = new List<RuledCell>();
        for (var r = 0; r < group.Count; r++)
        {
            var bandColumns = Columns(group[r].Members, gaps);
            for (var c = 0; c < edges.Length - 1; c++)
            {
                var cell = new RuledCell(r, c, 1, 1, new UglyToad.PdfPig.Core.PdfRectangle(edges[c], rows[r + 1], edges[c + 1], rows[r]));
                cell.Words.AddRange(bandColumns[c].SelectMany(i => i.Lines).SelectMany(l => l.Words));
                cells.Add(cell);
            }
        }
        var bounds = new UglyToad.PdfPig.Core.PdfRectangle(edges[0], rows[^1], edges[^1], rows[0]);
        return new RuledTable(bounds, edges, [.. rows], cells) { Layout = true };
    }

    /// <summary>
    /// Most rows of the column (the parts of a row split at a gap taken together: "Số:  …/CAT-ANM") have their middle at
    /// the middle of the column.
    /// </summary>
    private static bool Centred(List<Item> column)
    {
        var (left, right) = (column.Min(m => m.Left), column.Max(m => m.Right));
        var center = (left + right) / 2;
        var rows = column.SelectMany(m => m.Lines).GroupBy(l => l.Row)
            .Select(g => (g.Min(l => l.Left) + g.Max(l => l.Right)) / 2).ToList();
        var tolerance = Math.Max(3, 0.03 * (right - left));
        return rows.Count >= 2 && rows.Count(m => Math.Abs(m - center) <= tolerance) * 3 >= rows.Count * 2;
    }
}
