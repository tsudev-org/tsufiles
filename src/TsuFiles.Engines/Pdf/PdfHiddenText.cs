// A copy of a PDF whose pages draw everything but their text, for the exact layout of PDF → DOCX
// (PdfDocumentReader.Exact): PDFium renders that copy as the picture behind the page's words, so lines, boxes,
// fills, pictures, seals and signatures stay exactly where the PDF has them, and the words on top stay editable text.
//
// The text is not removed, only drawn invisibly: every text object starts with text rendering mode 3 (neither fill
// nor stroke) and every Tr operator is turned into 3, or 7 where the text clips (4-7: the clipping still applies, so a
// picture clipped by a word keeps its shape). The content streams are rewritten token by token and every other byte
// is kept as it is, inline images included. Annotation appearances (signatures, stamps, filled form fields) are not
// touched: their text is not among the page's words and stays part of the picture.

using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace TsuFiles.Engines.Pdf;

internal static class PdfHiddenText
{
    /// <summary>Form XObjects nested deeper than this are left as they are.</summary>
    private const int MaxDepth = 12;

    /// <summary>The PDF with all page text invisible, or null if it cannot be rewritten (the caller then falls back).</summary>
    public static byte[]? Create(byte[] pdf)
    {
        try
        {
            using var input = new MemoryStream(pdf, writable: false);
            using var document = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
            var done = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
            foreach (var page in document.Pages)
            {
                if (!HidePageText(document, page))
                    return null;
                Forms(Resources(page), done, 0);
            }
            using var output = new MemoryStream();
            document.Save(output, closeStream: false);
            return output.ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            return null; // encrypted, broken or unusual: the reader falls back to the flowing layout
        }
    }

    private static bool HidePageText(PdfDocument document, PdfPage page)
    {
        var parts = new List<byte[]>();
        switch (PdfCompressor.Resolve(page.Elements["/Contents"]))
        {
            case PdfDictionary { Stream: not null } single:
                parts.Add(single.Stream.UnfilteredValue);
                break;
            case PdfArray array:
                foreach (var item in array.Elements)
                {
                    if (PdfCompressor.Resolve(item) is PdfDictionary { Stream: not null } part)
                        parts.Add(part.Stream.UnfilteredValue);
                }
                break;
            case null:
                return true; // an empty page
            default:
                return false;
        }

        // The parts of /Contents are one stream split anywhere (even inside a text object): joined, then rewritten.
        using var joined = new MemoryStream();
        foreach (var part in parts)
        {
            joined.Write(part);
            joined.WriteByte((byte)'\n');
        }
        var content = new PdfDictionary(document);
        document.Internals.AddObject(content);
        content.CreateStream(Hide(joined.ToArray()));
        page.Elements["/Contents"] = content.Reference;
        return true;
    }

    /// <summary>The page's resources, inherited from the page tree when the page has none of its own.</summary>
    private static PdfDictionary? Resources(PdfPage page)
    {
        PdfDictionary? node = page;
        for (var depth = 0; node is not null && depth < 64; depth++)
        {
            if (PdfCompressor.Resolve(node.Elements["/Resources"]) is PdfDictionary resources)
                return resources;
            node = PdfCompressor.Resolve(node.Elements["/Parent"]) as PdfDictionary;
        }
        return null;
    }

    private static void Forms(PdfDictionary? resources, HashSet<PdfDictionary> done, int depth)
    {
        if (resources is null || depth > MaxDepth
            || PdfCompressor.Resolve(resources.Elements["/XObject"]) is not PdfDictionary xobjects)
            return;
        foreach (var key in xobjects.Elements.Keys.ToList())
        {
            if (PdfCompressor.Resolve(xobjects.Elements[key]) is not PdfDictionary { Stream: not null } form
                || form.Elements.GetName("/Subtype") != "/Form" || !done.Add(form))
                continue;
            var hidden = Hide(form.Stream.UnfilteredValue);
            form.Elements.Remove("/Filter");
            form.Elements.Remove("/DecodeParms");
            form.Stream.Value = hidden;
            form.Elements.SetInteger("/Length", hidden.Length);
            Forms(PdfCompressor.Resolve(form.Elements["/Resources"]) as PdfDictionary, done, depth + 1);
        }
    }

    // ───────────────────────── Content stream rewriting ─────────────────────────

    private static bool IsWhite(byte b) => b is 0 or 9 or 10 or 12 or 13 or 32;

    private static bool IsDelimiter(byte b) => b is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']'
        or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

    /// <summary>
    /// <paramref name="content"/> with " 3 Tr" after every BT and the operand of every Tr replaced by 3 (or 7 for the
    /// clipping modes 4-7). Strings, names, comments and inline image data are copied unchanged.
    /// </summary>
    internal static byte[] Hide(byte[] content)
    {
        var output = new List<byte>(content.Length + 256);
        var i = 0;
        var lastNumberStart = -1; // where the latest number token starts in output, -1 if the latest token is no number
        while (i < content.Length)
        {
            var b = content[i];
            if (IsWhite(b))
            {
                output.Add(b);
                i++;
                continue;
            }
            switch (b)
            {
                case (byte)'%':
                    while (i < content.Length && content[i] is not (10 or 13))
                        output.Add(content[i++]);
                    continue;
                case (byte)'(':
                    i = CopyString(content, i, output);
                    lastNumberStart = -1;
                    continue;
                case (byte)'<' when i + 1 < content.Length && content[i + 1] == '<':
                case (byte)'>' when i + 1 < content.Length && content[i + 1] == '>':
                    output.Add(content[i++]);
                    output.Add(content[i++]);
                    lastNumberStart = -1;
                    continue;
                case (byte)'<':
                    while (i < content.Length && content[i] != '>')
                        output.Add(content[i++]);
                    if (i < content.Length)
                        output.Add(content[i++]);
                    lastNumberStart = -1;
                    continue;
                case (byte)'/':
                    output.Add(content[i++]);
                    while (i < content.Length && !IsWhite(content[i]) && !IsDelimiter(content[i]))
                        output.Add(content[i++]);
                    lastNumberStart = -1;
                    continue;
            }
            if (IsDelimiter(b))
            {
                output.Add(b);
                i++;
                lastNumberStart = -1;
                continue;
            }

            var start = i;
            while (i < content.Length && !IsWhite(content[i]) && !IsDelimiter(content[i]))
                i++;
            var token = content.AsSpan(start, i - start);
            if (token.SequenceEqual("BT"u8))
            {
                output.AddRange("BT 3 Tr"u8.ToArray());
                lastNumberStart = -1;
            }
            else if (token.SequenceEqual("Tr"u8) && lastNumberStart >= 0)
            {
                var mode = ParseMode(output, lastNumberStart);
                output.RemoveRange(lastNumberStart, output.Count - lastNumberStart);
                output.AddRange(mode >= 4 ? "7 Tr"u8.ToArray() : "3 Tr"u8.ToArray());
                lastNumberStart = -1;
            }
            else if (token.SequenceEqual("ID"u8))
            {
                output.AddRange(token.ToArray());
                i = CopyInlineImage(content, i, output);
                lastNumberStart = -1;
            }
            else
            {
                var isNumber = IsNumber(token);
                output.AddRange(token.ToArray());
                lastNumberStart = isNumber ? output.Count - token.Length : -1;
            }
        }
        return [.. output];
    }

    private static bool IsNumber(ReadOnlySpan<byte> token)
    {
        if (token.IsEmpty)
            return false;
        foreach (var c in token)
        {
            if (c is not ((>= (byte)'0' and <= (byte)'9') or (byte)'.' or (byte)'-' or (byte)'+'))
                return false;
        }
        return true;
    }

    private static int ParseMode(List<byte> output, int start)
    {
        var text = System.Text.Encoding.ASCII.GetString(output.GetRange(start, output.Count - start).ToArray()).Trim();
        return double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? (int)value : 0;
    }

    /// <summary>A literal string with its nested parentheses and escapes; returns the index after it.</summary>
    private static int CopyString(byte[] content, int i, List<byte> output)
    {
        var depth = 0;
        while (i < content.Length)
        {
            var c = content[i++];
            output.Add(c);
            if (c == '\\')
            {
                if (i < content.Length)
                    output.Add(content[i++]);
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')' && --depth == 0)
            {
                break;
            }
        }
        return i;
    }

    /// <summary>The binary data after ID up to and including the EI that ends it; returns the index after EI.</summary>
    private static int CopyInlineImage(byte[] content, int i, List<byte> output)
    {
        // One white-space byte follows ID, then the data; EI ends it when white space precedes it and white space, a
        // delimiter or the end of the stream follows.
        var start = i;
        for (var j = i + 1; j + 1 < content.Length; j++)
        {
            if (content[j] == 'E' && content[j + 1] == 'I' && IsWhite(content[j - 1])
                && (j + 2 >= content.Length || IsWhite(content[j + 2]) || IsDelimiter(content[j + 2])))
            {
                output.AddRange(content.AsSpan(start, j + 2 - start).ToArray());
                return j + 2;
            }
        }
        output.AddRange(content.AsSpan(start).ToArray());
        return content.Length;
    }
}
