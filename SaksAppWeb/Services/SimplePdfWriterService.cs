using System.Text;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace SaksAppWeb.Services;

public sealed class SimplePdfWriter
{
    private readonly PdfDocument _doc = new();
    private PdfPage _page;
    private XGraphics _gfx;
    private double _y;

    private readonly XFont _titleFont = new("Verdana", 16, XFontStyle.Bold);
    private readonly XFont _hFont = new("Verdana", 12, XFontStyle.Bold);
    private readonly XFont _pFont = new("Verdana", 10, XFontStyle.Regular);

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
    public void Paragraph(string text) => WriteWrapped(text, _pFont, extraBottom: 6);

    public void Blank(double points = 8) => _y += points;

    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        _doc.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    private void WriteWrapped(string text, XFont font, double extraBottom)
    {
        foreach (var line in Wrap(text, font, _page.Width - 2 * Margin))
        {
            EnsureSpace(font.GetHeight() + LineGap);
            _gfx.DrawString(line, font, XBrushes.Black, new XRect(Margin, _y, _page.Width - 2 * Margin, _page.Height), XStringFormats.TopLeft);
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
