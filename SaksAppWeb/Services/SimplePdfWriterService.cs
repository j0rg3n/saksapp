using System.Text;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Pdf.Annotations;

namespace SaksAppWeb.Services;

public sealed class SimplePdfWriter
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

    public SimplePdfWriter()
    {
        _page = _doc.AddPage();
        _gfx = XGraphics.FromPdfPage(_page);
        _y = Margin;
    }

    public void Title(string text) => WriteWrapped(text, _titleFont, extraBottom: 12);
    public void Heading(string text) => WriteWrapped(text, _hFont, extraBottom: 8);
    public void Heading2(string text) => WriteWrapped(text, _h2Font, extraBottom: 6);
    public void Heading3(string text) => WriteWrapped(text, _h3Font, extraBottom: 4);
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

    public void HeadingInline(string heading, string text)
    {
        WriteWrapped(heading, _hFont, extraBottom: 0);
        WriteWrapped(text, _pFont, extraBottom: 4, indent: true);
    }

    public void WriteTextWithAttachmentLinks(string text, Dictionary<int, int> attachmentPageNumbers)
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

            // Get attachment number and write as blue "link" text
            var attNum = int.Parse(match.Groups[1].Value);
            if (attachmentPageNumbers.TryGetValue(attNum, out var targetPageNum) && targetPageNum > 0)
            {
                WriteLinkText($"[Vedlegg {attNum}]", font, targetPageNum);
            }
            else
            {
                WriteWrapped($"[Vedlegg {attNum}]", font, extraBottom: 0);
            }

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

    private void WriteLinkText(string text, XFont font, int targetPageNum)
    {
        // Draw blue text to indicate it's a link (actual clickable links require more complex PdfSharpCore setup)
        var fontHeight = font.GetHeight();
        var rect = new XRect(Margin, _y, _page.Width - 2 * Margin, fontHeight + LineGap);
        _gfx.DrawString(text, font, XBrushes.DarkBlue, rect, XStringFormats.TopLeft);
        _y += fontHeight + LineGap;
    }

    public Dictionary<int, int> GetAttachmentPageNumbers() => _attachmentPageNumbers;

    private readonly Dictionary<int, int> _attachmentPageNumbers = new();

    public void Blank(double points = 8) => _y += points;

    public void AddPdfAttachment(byte[] pdfContent, string fileName, int number)
    {
        var pageNum = AddAttachmentCoverPage(fileName, number);

        using var inputStream = new MemoryStream(pdfContent);
        var inputDoc = PdfReader.Open(inputStream, PdfDocumentOpenMode.Import);

        for (int i = 0; i < inputDoc.PageCount; i++)
        {
            var page = inputDoc.Pages[i];
            _doc.AddPage(page);
        }
    }

    private int AddAttachmentCoverPage(string fileName, int number)
    {
        var page = _doc.AddPage();
        _attachmentPageNumbers[number] = _doc.PageCount;
        _gfx = XGraphics.FromPdfPage(page);
        _page = page;
        _y = Margin;

        _gfx.DrawString($"Vedlegg {number}:", _hFont, XBrushes.Black, new XRect(Margin, _y, _page.Width - 2 * Margin, _page.Height), XStringFormats.TopLeft);
        _y += _hFont.GetHeight() + LineGap;

        _gfx.DrawString(fileName, _attachmentTitleFont, XBrushes.Black, new XRect(Margin, _y, _page.Width - 2 * Margin, _page.Height), XStringFormats.TopLeft);

        return _doc.PageCount;
    }

    public void AddImageAttachment(byte[] imageContent, string fileName, int number)
    {
        var page = _doc.AddPage();
        _attachmentPageNumbers[number] = _doc.PageCount;
        _gfx = XGraphics.FromPdfPage(page);
        _page = page;
        _y = Margin;

        _gfx.DrawString($"Vedlegg {number}: {fileName}", _hFont, XBrushes.Black, new XRect(Margin, _y, _page.Width - 2 * Margin, _page.Height), XStringFormats.TopLeft);
        _y += _hFont.GetHeight() + LineGap;

        using var imageStream = new MemoryStream(imageContent);
        var image = XImage.FromStream(() => new MemoryStream(imageContent));

        var availableWidth = _page.Width - 2 * Margin;
        var availableHeight = _page.Height - _y - Margin;

        var ratio = Math.Min(availableWidth / image.PixelWidth, availableHeight / image.PixelHeight);
        var drawWidth = image.PixelWidth * ratio;
        var drawHeight = image.PixelHeight * ratio;

        var x = Margin;
        var y = _y;

        _gfx.DrawImage(image, x, y, drawWidth, drawHeight);
    }

    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        _doc.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    private const double IndentWidth = 20;

    private void WriteWrapped(string text, XFont font, double extraBottom, bool indent = false)
    {
        var xOffset = indent ? Margin + IndentWidth : Margin;
        var maxWidth = _page.Width - xOffset - Margin;

        foreach (var line in Wrap(text, font, maxWidth))
        {
            EnsureSpace(font.GetHeight() + LineGap);
            _gfx.DrawString(line, font, XBrushes.Black, new XRect(xOffset, _y, maxWidth, _page.Height), XStringFormats.TopLeft);
            _y += font.GetHeight() + LineGap;
        }

        _y += extraBottom;
    }

    private void EnsureSpace(double neededHeight)
    {
        if (_y + neededHeight < _page.Height - Margin)
            return;

        _page = _doc.AddPage();
        _gfx = XGraphics.FromPdfPage(_page);
        _y = Margin;
    }

    private IEnumerable<string> Wrap(string text, XFont font, double maxWidth)
    {
        text ??= "";
        var paragraphs = text.Replace("\r\n", "\n").Split('\n');

        foreach (var p in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(p))
            {
                yield return "";
                continue;
            }

            var words = p.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();

            foreach (var w in words)
            {
                var candidate = sb.Length == 0 ? w : sb + " " + w;
                var width = _gfx.MeasureString(candidate, font).Width;

                if (width <= maxWidth)
                {
                    sb.Clear();
                    sb.Append(candidate);
                }
                else
                {
                    if (sb.Length > 0)
                        yield return sb.ToString();

                    sb.Clear();
                    sb.Append(w);
                }
            }

            if (sb.Length > 0)
                yield return sb.ToString();
        }
    }
}
