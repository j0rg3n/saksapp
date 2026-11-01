using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Pdf.Annotations;

namespace SaksAppWeb.Services;

public sealed class SimplePdfWriter : ISimplePdfWriter
{
    private readonly PdfDocument _doc = new();
    private PdfPage _page;
    private XGraphics _gfx;
    private double _y;

    private readonly XFont _titleFont = new("LiberationSans", 16, XFontStyle.Bold);
    private readonly XFont _hFont = new("LiberationSans", 12, XFontStyle.Bold);
    private readonly XFont _h2Font = new("LiberationSans", 11, XFontStyle.Bold);
    private readonly XFont _h3Font = new("LiberationSans", 10, XFontStyle.Bold);
    private readonly XFont _pFont = new("LiberationSans", 10, XFontStyle.Regular);
    private readonly XFont _pItalicFont = new("LiberationSans", 10, XFontStyle.Italic);
    private readonly XFont _attachmentTitleFont = new("LiberationSans", 14, XFontStyle.Bold);

    private const double Margin = 40;
    private const double LineGap = 4;

    private class PendingLink
    {
        public int PageNumber { get; init; }
        public double Y { get; init; }
        public double Width { get; init; }
        public double Height { get; init; }
        public int AttachmentNumber { get; init; }
    }

    private class PendingTocLink
    {
        public int PageNumber { get; init; }
        public double Y { get; init; }
        public double Width { get; init; }
        public double Height { get; init; }
        public int TargetPageNumber { get; init; }
    }

    /// <summary>
    /// A TOC line written before its attachment existed, so its page number is not yet known.
    /// Both the printed number and the link target are filled in by
    /// <see cref="ApplyAttachmentTocPageNumbers"/> once the attachments have been appended.
    /// </summary>
    private sealed class DeferredTocPageNumber
    {
        /// <summary>
        /// The graphics context that was live for this page. PdfSharp allows only one XGraphics per
        /// page at a time and the writer never disposes them, so the original must be reused.
        /// </summary>
        public required XGraphics Gfx { get; init; }
        public required int TocPageNumber { get; init; }
        public required double Y { get; init; }
        public required double Width { get; init; }
        public required double Height { get; init; }
        public required int AttachmentNumber { get; init; }
    }

    private readonly List<DeferredTocPageNumber> _deferredTocPageNumbers = new();
    private readonly List<PendingLink> _pendingLinks = new();
    private readonly List<PendingTocLink> _pendingTocLinks = new();
    private readonly Dictionary<int, int> _attachmentPageNumbers = new();

    private readonly ILogger _logger;

    public SimplePdfWriter(ILogger<SimplePdfWriter>? logger = null)
    {
        _logger = logger ?? NullLogger<SimplePdfWriter>.Instance;
        _page = _doc.AddPage();
        _gfx = XGraphics.FromPdfPage(_page);
        _y = Margin;
    }

    public void Title(string text) => WriteWrapped(text, _titleFont, extraBottom: 12);
    public void Heading(string text) => WriteHeading(text, _hFont, extraBottom: 8);
    public void Heading2(string text) => WriteHeading(text, _h2Font, extraBottom: 6);
    public void Heading3(string text) => WriteHeading(text, _h3Font, extraBottom: 4);

    /// <summary>Lines of body text a heading must be able to bring with it onto its page.</summary>
    private const int KeepWithNextLines = 2;

    private double BodyLineHeight => _pFont.GetHeight() + LineGap;

    /// <summary>Starts a new page unless the heading fits together with the start of what follows.</summary>
    private void KeepWithNext(XFont headingFont) =>
        EnsureSpace(headingFont.GetHeight() + LineGap + KeepWithNextLines * BodyLineHeight);

    private void WriteHeading(string text, XFont font, double extraBottom)
    {
        KeepWithNext(font);
        WriteWrapped(text, font, extraBottom);
    }
    public void Paragraph(string text) => WriteWrapped(text, _pFont, extraBottom: 6);
    public void ParagraphItalic(string text) => WriteWrapped(text, _pItalicFont, extraBottom: 6);

    public void ParagraphIndented(string text) => WriteWrapped(text, _pFont, extraBottom: 6, indent: true);
    public void ParagraphItalicIndented(string text) => WriteWrapped(text, _pItalicFont, extraBottom: 6, indent: true);

    public void ParagraphFirstLine(string firstLine, string continuation, bool isItalic = false)
    {
        var font = isItalic ? _pItalicFont : _pFont;
        WriteWrapped(firstLine, font, extraBottom: 0);
        if (!string.IsNullOrWhiteSpace(continuation))
            WriteWrapped(continuation, font, extraBottom: 6, indent: true);
    }

    /// <summary>
    /// Writes "&lt;date&gt; [badge] &lt;text&gt;". The badge sits inline after the date — a badge on its
    /// own line costs a whole line per entry, which is wasteful in a long history list — and the
    /// header has the same shape with or without a badge (no colon in either case).
    ///
    /// The text fills whatever is left of the first line, measured; it used to be cut at a fixed
    /// character count by the caller, which left first lines at roughly half width.
    /// </summary>
    public void HistoryEntry(string date, string? badgeLabel, string? badgeColorHex, string text)
    {
        var font = _pItalicFont;
        var lineHeight = font.GetHeight() + LineGap;

        // Room for the header line plus one more, so an entry never starts on a page's last line.
        EnsureSpace(2 * lineHeight);

        var x = Margin;
        var dateWidth = _gfx.MeasureString(date, font).Width;
        _gfx.DrawString(date, font, XBrushes.Black,
            new XRect(x, _y, dateWidth, lineHeight), XStringFormats.TopLeft);
        x += dateWidth + 5;

        if (!string.IsNullOrEmpty(badgeLabel) && !string.IsNullOrEmpty(badgeColorHex))
            x += DrawInlineBadge(badgeLabel, badgeColorHex, x, _y);

        if (string.IsNullOrWhiteSpace(text))
        {
            _y += lineHeight + 6;
            return;
        }

        WriteFlow(text, font, firstX: x, restX: Margin + IndentWidth, extraBottom: 6, firstLinePlaced: true);
    }

    /// <summary>Draws a coloured dot plus label at (x, y). Returns the horizontal space consumed.</summary>
    private double DrawInlineBadge(string label, string colorHex, double x, double y)
    {
        var r = Convert.ToInt32(colorHex.Substring(1, 2), 16);
        var g = Convert.ToInt32(colorHex.Substring(3, 2), 16);
        var b = Convert.ToInt32(colorHex.Substring(5, 2), 16);
        var brush = new XSolidBrush(XColor.FromArgb(r, g, b));

        var fontHeight = _pFont.GetHeight();
        var radius = fontHeight * 0.28;
        const double dotToLabel = 3;
        const double labelToText = 6;

        _gfx.DrawEllipse(brush, x, y + fontHeight * 0.25, radius * 2, radius * 2);

        var labelX = x + radius * 2 + dotToLabel;
        var labelWidth = _gfx.MeasureString(label, _h3Font).Width;
        _gfx.DrawString(label, _h3Font, XBrushes.Black,
            new XRect(labelX, y, labelWidth, fontHeight + LineGap), XStringFormats.TopLeft);

        return radius * 2 + dotToLabel + labelWidth + labelToText;
    }

    /// <summary>
    /// A sub-label under an agenda item (Status:, Vedtak:, …) with its text indented beneath.
    /// The label uses the same 10 pt Bold as the other sub-labels; it was 12 pt, larger than the
    /// 11 pt item title it belongs to.
    /// </summary>
    public void HeadingInline(string heading, string text)
    {
        KeepWithNext(_h3Font);
        WriteWrapped(heading, _h3Font, extraBottom: 0);
        WriteWrapped(text, _pFont, extraBottom: 4, indent: true);
    }

    public void WriteTextWithAttachmentLinks(string text)
    {
        var font = _pFont;
        
        // Find all [Vedlegg N] patterns and style them as links (blue)
        var pattern = @"\[Vedlegg (\d+)\]";
        var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern);
        
        if (matches.Count == 0)
        {
            WriteWrapped(text, font, extraBottom: 6);
            return;
        }

        var lastEnd = 0;
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            // Write text before the match
            if (match.Index > lastEnd)
            {
                var beforeText = text.Substring(lastEnd, match.Index - lastEnd);
                WriteWrapped(beforeText, font, extraBottom: 0);
            }

            // Get attachment number - we'll look up page number later
            var attNum = int.Parse(match.Groups[1].Value);
            WriteLinkText($"[Vedlegg {attNum}]", font, attNum);

            lastEnd = match.Index + match.Length;
        }

        // Write remaining text after last match
        if (lastEnd < text.Length)
        {
            var afterText = text.Substring(lastEnd);
            WriteWrapped(afterText, font, extraBottom: 6);
        }
        else
        {
            _y += 6;
        }
    }

    private void WriteLinkText(string text, XFont font, int attachmentNumber)
    {
        var fontHeight = font.GetHeight();
        var textWidth = _gfx.MeasureString(text, font).Width;
        
        // Draw blue text
        var rect = new XRect(Margin, _y, textWidth, fontHeight + LineGap);
        _gfx.DrawString(text, font, XBrushes.DarkBlue, rect, XStringFormats.TopLeft);
        
        // Store for later annotation creation
        _pendingLinks.Add(new PendingLink
        {
            PageNumber = _doc.PageCount,
            Y = _y,
            Width = textWidth,
            Height = fontHeight + LineGap,
            AttachmentNumber = attachmentNumber
        });
        
        _y += fontHeight + LineGap;
    }

    public void ApplyPendingLinks()
    {
        foreach (var link in _pendingLinks)
        {
            if (!_attachmentPageNumbers.TryGetValue(link.AttachmentNumber, out var actualTargetPage))
            {
                continue;
            }
            
            var pageHeight = _doc.Pages[link.PageNumber - 1].Height;
            var pdfY = pageHeight - link.Y - link.Height;
            
            var pdfRect = new PdfRectangle(new XPoint(link.Y, pdfY), new XPoint(link.Y + link.Width, pdfY + link.Height));
            
            var annotation = PdfLinkAnnotation.CreateDocumentLink(pdfRect, actualTargetPage - 1);
            _doc.Pages[link.PageNumber - 1].Annotations.Add(annotation);
        }
    }

    public Dictionary<int, int> GetAttachmentPageNumbers() => _attachmentPageNumbers;

    public int GetCurrentPageNumber() => _doc.PageCount;

    public void Blank(double points = 8) => _y += points;

    public void WriteAttachmentTocEntry(int pageNumber, int attachmentNumber, string fileName)
    {
        var text = $"{pageNumber}: Vedlegg {attachmentNumber}: {fileName}";
        var fontHeight = _pFont.GetHeight();
        var textWidth = _gfx.MeasureString(text, _pFont).Width;

        _gfx.DrawString(text, _pFont, XBrushes.Black, new XRect(Margin, _y, textWidth, fontHeight + LineGap), XStringFormats.TopLeft);

        _pendingTocLinks.Add(new PendingTocLink
        {
            PageNumber = _doc.PageCount,
            Y = _y,
            Width = textWidth,
            Height = fontHeight + LineGap,
            TargetPageNumber = pageNumber
        });

        _y += fontHeight + LineGap + 4;
    }

    public void WriteAttachmentTocEntryLabel(int pageNumber, string label, string fileName)
    {
        var text = $"{pageNumber}: Vedlegg {label}: {fileName}";
        var fontHeight = _pFont.GetHeight();
        var textWidth = _gfx.MeasureString(text, _pFont).Width;

        _gfx.DrawString(text, _pFont, XBrushes.Black, new XRect(Margin, _y, textWidth, fontHeight + LineGap), XStringFormats.TopLeft);

        _pendingTocLinks.Add(new PendingTocLink
        {
            PageNumber = _doc.PageCount,
            Y = _y,
            Width = textWidth,
            Height = fontHeight + LineGap,
            TargetPageNumber = pageNumber
        });

        _y += fontHeight + LineGap + 4;
    }

    public void OutcomeBadge(string label, string colorHex)
    {
        var r = Convert.ToInt32(colorHex.Substring(1, 2), 16);
        var g = Convert.ToInt32(colorHex.Substring(3, 2), 16);
        var b = Convert.ToInt32(colorHex.Substring(5, 2), 16);
        var color = XColor.FromArgb(r, g, b);
        var brush = new XSolidBrush(color);

        var fontHeight = _pFont.GetHeight();
        var circleRadius = fontHeight * 0.4;
        var circleX = Margin + circleRadius;
        var circleY = _y + fontHeight * 0.1;

        EnsureSpace(fontHeight + LineGap);
        _gfx.DrawEllipse(brush, circleX - circleRadius, circleY, circleRadius * 2, circleRadius * 2);

        var textX = Margin + circleRadius * 2 + 4;
        var textRect = new XRect(textX, _y, _page.Width - textX - Margin, fontHeight + LineGap);
        _gfx.DrawString(label, _pFont, XBrushes.Black, textRect, XStringFormats.TopLeft);
        _y += fontHeight + LineGap + 4;
    }

    /// <summary>
    /// Writes a TOC line, reserving a right-hand column for the page number. Writing the number up
    /// front cannot work: the attachments have not been appended yet, so every entry would receive
    /// the same value — which is exactly the bug this replaces.
    ///
    /// The page number sits at the right margin as "s. n"; it used to be a "n:" prefix on the left,
    /// which readers took for a stray row ID.
    /// </summary>
    public void WriteAttachmentTocEntryDeferred(int attachmentNumber, string label, string fileName)
    {
        var lineHeight = _pFont.GetHeight() + LineGap;
        var lineWidth = _page.Width - 2 * Margin;
        var pageColumnWidth = _gfx.MeasureString("s. 0000", _pFont).Width + 12;
        var text = Ellipsize($"Vedlegg {label}: {Clean(fileName)}", _pFont, lineWidth - pageColumnWidth);

        EnsureSpace(lineHeight);

        _gfx.DrawString(text, _pFont, XBrushes.Black,
            new XRect(Margin, _y, lineWidth - pageColumnWidth, lineHeight), XStringFormats.TopLeft);

        _deferredTocPageNumbers.Add(new DeferredTocPageNumber
        {
            Gfx = _gfx,
            TocPageNumber = _doc.PageCount,
            Y = _y,
            Width = lineWidth,
            Height = lineHeight,
            AttachmentNumber = attachmentNumber
        });

        _y += lineHeight + 4;
    }

    /// <summary>
    /// Fills each reserved page-number column with its attachment's real start page, and registers
    /// the TOC hyperlink against that page. Call once every attachment has been appended.
    /// </summary>
    public void ApplyAttachmentTocPageNumbers()
    {
        foreach (var entry in _deferredTocPageNumbers)
        {
            if (!_attachmentPageNumbers.TryGetValue(entry.AttachmentNumber, out var pageNumber))
                continue;

            entry.Gfx.DrawString($"s. {pageNumber}", _pFont, XBrushes.Black,
                new XRect(Margin, entry.Y, entry.Width, entry.Height), XStringFormats.TopRight);

            // The link target was previously the same guessed page for every entry, so the TOC
            // hyperlinks were as wrong as the printed numbers.
            _pendingTocLinks.Add(new PendingTocLink
            {
                PageNumber = entry.TocPageNumber,
                Y = entry.Y,
                Width = entry.Width,
                Height = entry.Height,
                TargetPageNumber = pageNumber
            });
        }

        _deferredTocPageNumbers.Clear();
    }

    public int AddPdfAttachmentStart()
    {
        return _doc.PageCount + 1;
    }

    public void AddPdfAttachment(byte[] pdfContent, string fileName, int number, string label)
    {
        StartAttachmentPage(fileName, number, label);

        // Remembered so a failure part-way through the import can be rolled back: an attachment is
        // either included whole or not at all.
        var pageCountBeforeImport = _doc.PageCount;

        try
        {
            using var inputStream = new MemoryStream(pdfContent);
            var inputDoc = PdfReader.Open(inputStream, PdfDocumentOpenMode.Import);

            for (int i = 0; i < inputDoc.PageCount; i++)
            {
                _doc.AddPage(inputDoc.Pages[i]);
            }
        }
        catch (Exception ex) when (IsAttachmentParseFailure(ex))
        {
            while (_doc.PageCount > pageCountBeforeImport)
                _doc.Pages.RemoveAt(_doc.PageCount - 1);

            WriteAttachmentUnavailable(ex, fileName, number);
        }
    }

    /// <summary>
    /// Opens a new page for an attachment and writes its caption — the same for PDF and image
    /// attachments. The caption names the attachment by its label ("Vedlegg 2.1"), the identifier
    /// used in the text and the index; it used to show the internal sequence number instead.
    /// </summary>
    private void StartAttachmentPage(string fileName, int number, string label)
    {
        _page = _doc.AddPage();
        _attachmentPageNumbers[number] = _doc.PageCount;
        _gfx = XGraphics.FromPdfPage(_page);
        _y = Margin;

        WriteWrapped($"Vedlegg {label}", _hFont, extraBottom: 0);
        WriteWrapped(fileName, _attachmentTitleFont, extraBottom: LineGap);
    }

    public void AddImageAttachment(byte[] imageContent, string fileName, int number, string label)
    {
        StartAttachmentPage(fileName, number, label);

        try
        {
            // The decoder may defer reading the bytes, so everything up to DrawImage stays inside.
            var image = XImage.FromStream(() => new MemoryStream(imageContent));

            var availableWidth = _page.Width - 2 * Margin;
            var availableHeight = _page.Height - _y - Margin;

            var ratio = Math.Min(availableWidth / image.PixelWidth, availableHeight / image.PixelHeight);
            var drawWidth = image.PixelWidth * ratio;
            var drawHeight = image.PixelHeight * ratio;

            _gfx.DrawImage(image, Margin, _y, drawWidth, drawHeight);
        }
        catch (Exception ex) when (IsAttachmentParseFailure(ex))
        {
            WriteAttachmentUnavailable(ex, fileName, number);
        }
    }

    /// <summary>
    /// Attachments are user-uploaded bytes fed to third-party parsers, which throw a wide and
    /// undocumented range of exception types on malformed input (PdfReaderException,
    /// InvalidOperationException, IndexOutOfRangeException, image-decoder exceptions, …), so the
    /// catch cannot be narrowed by type without letting some corrupt files crash the export again.
    /// It is narrowed by scope instead — only the parse/import calls are inside the try — and
    /// process-level failures are never swallowed.
    /// </summary>
    private static bool IsAttachmentParseFailure(Exception ex) =>
        ex is not (OutOfMemoryException or StackOverflowException);

    private void WriteAttachmentUnavailable(Exception ex, string fileName, int number)
    {
        _logger.LogWarning(ex, "Attachment {Number} ({FileName}) could not be embedded in the PDF", number, fileName);

        _y += LineGap * 2;
        WriteWrapped(
            "Vedlegget kunne ikke settes inn i dokumentet fordi filen ikke kunne leses. " +
            "Originalfilen ligger i saken i SaksApp.",
            _pItalicFont, extraBottom: 0);
    }

    public byte[] ToBytes()
    {
        ApplyPendingLinks();
        ApplyPendingTocLinks();
        
        using var ms = new MemoryStream();
        _doc.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    private void ApplyPendingTocLinks()
    {
        foreach (var link in _pendingTocLinks)
        {
            var pageHeight = _doc.Pages[link.PageNumber - 1].Height;
            var pdfY = pageHeight - link.Y - link.Height;

            // The X coordinates were taken from link.Y, which put the clickable box at a horizontal
            // offset equal to the line's vertical position. The line starts at the left margin.
            var pdfRect = new PdfRectangle(
                new XPoint(Margin, pdfY),
                new XPoint(Margin + link.Width, pdfY + link.Height));

            // CreateDocumentLink takes a one-based page number, and TargetPageNumber already is one,
            // so the previous "- 1" landed every link one page early.
            var annotation = PdfLinkAnnotation.CreateDocumentLink(pdfRect, link.TargetPageNumber);
            _doc.Pages[link.PageNumber - 1].Annotations.Add(annotation);
        }
    }

    private const double IndentWidth = 20;

    private void WriteWrapped(string text, XFont font, double extraBottom, bool indent = false)
    {
        var x = indent ? Margin + IndentWidth : Margin;
        WriteFlow(text, font, firstX: x, restX: x, extraBottom, firstLinePlaced: false);
    }

    /// <summary>
    /// Writes text paragraph by paragraph (split on newlines). The first line of the first
    /// paragraph starts at <paramref name="firstX"/>, everything else at <paramref name="restX"/>;
    /// a paragraph that starts with a list marker wraps its continuation lines under the text, not
    /// under the marker. With <paramref name="firstLinePlaced"/> the caller has already reserved
    /// space for the first line and drawn something on it, so no page break may happen before it.
    /// </summary>
    private void WriteFlow(string text, XFont font, double firstX, double restX, double extraBottom, bool firstLinePlaced)
    {
        var lineHeight = font.GetHeight() + LineGap;
        var paragraphs = Clean(text).Replace("\r\n", "\n").Split('\n');

        for (var p = 0; p < paragraphs.Length; p++)
        {
            var startX = p == 0 ? firstX : restX;
            var continuationX = restX + ListMarkerHangWidth(paragraphs[p], font);
            var lines = WrapParagraph(paragraphs[p], font,
                firstWidth: _page.Width - startX - Margin,
                restWidth: _page.Width - continuationX - Margin);

            for (var i = 0; i < lines.Count; i++)
            {
                // A paragraph ending in ':' is a label for what follows ("Plan:"),
                // so it must not be left alone at the bottom of a page.
                var isLabel = i == lines.Count - 1 && lines[i].EndsWith(':');
                if (!(firstLinePlaced && p == 0 && i == 0))
                    EnsureSpace(isLabel ? lineHeight + KeepWithNextLines * BodyLineHeight : lineHeight);

                var x = i == 0 ? startX : continuationX;
                _gfx.DrawString(lines[i], font, XBrushes.Black,
                    new XRect(x, _y, _page.Width - x - Margin, lineHeight), XStringFormats.TopLeft);
                _y += lineHeight;
            }
        }

        _y += extraBottom;
    }

    private static readonly Regex ListMarker = new(@"^\s*(\d{1,3}[.)]|[-*•–])\s+", RegexOptions.Compiled);

    /// <summary>Width of a leading list marker ("1. ", "- ", "• "), or 0 when there is none.</summary>
    private double ListMarkerHangWidth(string paragraph, XFont font)
    {
        var m = ListMarker.Match(paragraph);
        return m.Success ? _gfx.MeasureString(m.Groups[1].Value + " ", font).Width : 0;
    }

    /// <summary>
    /// The embedded font (Liberation Sans) has no emoji or pictographs; drawn anyway they come out
    /// as blank boxes. Covers astral-plane characters (all modern emoji), Misc Symbols and Dingbats
    /// (U+2600–U+27BF), variation selectors, ZWJ and the keycap combiner. One preceding space goes
    /// too, so "tekst 🔒." becomes "tekst." rather than "tekst .".
    /// </summary>
    private static readonly Regex UnsupportedChars = new(
        @" ?(?:[\uD800-\uDBFF][\uDC00-\uDFFF]|[☀-➿︎️‍⃣])+",
        RegexOptions.Compiled);

    public static string Clean(string? text) => UnsupportedChars.Replace(text ?? "", "");

    /// <summary>
    /// Greedy word wrap of one paragraph. The first line may have a different width from the rest
    /// (text following a date and badge, or a hanging indent). A word wider than a whole line —
    /// typically a long file name — is broken inside the word rather than run off the page.
    /// </summary>
    private List<string> WrapParagraph(string paragraph, XFont font, double firstWidth, double restWidth)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(paragraph))
        {
            lines.Add("");
            return lines;
        }

        double Measure(string s) => _gfx.MeasureString(s, font).Width;
        double CurrentWidth() => lines.Count == 0 ? firstWidth : restWidth;

        var current = "";
        foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (Measure(candidate) <= CurrentWidth())
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current);
                current = "";
            }

            var rest = word;
            while (Measure(rest) > CurrentWidth())
            {
                // A short first line (after a wide date and badge) that cannot hold the word: start
                // the word on the next line instead of splitting a word that would fit there.
                if (lines.Count == 0 && Measure(rest) <= restWidth)
                {
                    lines.Add("");
                    continue;
                }

                var cut = 1;
                while (cut < rest.Length && Measure(rest[..(cut + 1)]) <= CurrentWidth())
                    cut++;
                lines.Add(rest[..cut]);
                rest = rest[cut..];
            }
            current = rest;
        }

        if (current.Length > 0)
            lines.Add(current);
        return lines;
    }

    /// <summary>Shortens text with a trailing "…" until it fits <paramref name="maxWidth"/>.</summary>
    private string Ellipsize(string text, XFont font, double maxWidth)
    {
        if (_gfx.MeasureString(text, font).Width <= maxWidth)
            return text;

        var keep = text.Length;
        while (keep > 1 && _gfx.MeasureString(text[..keep] + "…", font).Width > maxWidth)
            keep--;
        return text[..keep].TrimEnd() + "…";
    }

    private void EnsureSpace(double neededHeight)
    {
        if (_y + neededHeight < _page.Height - Margin)
            return;

        _page = _doc.AddPage();
        _gfx = XGraphics.FromPdfPage(_page);
        _y = Margin;
    }

}
