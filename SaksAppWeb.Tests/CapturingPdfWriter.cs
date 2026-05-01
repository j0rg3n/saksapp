using SaksAppWeb.Services;

namespace SaksAppWeb.Tests;

public sealed class CapturingPdfWriter : ISimplePdfWriter
{
    public List<string> Calls { get; } = new();
    public byte[] ResultBytes { get; set; } = new byte[] { 1, 2, 3 };

    public void Title(string text) => Calls.Add($"Title:{text}");
    public void Heading(string text) => Calls.Add($"Heading:{text}");
    public void Heading2(string text) => Calls.Add($"Heading2:{text}");
    public void Heading3(string text) => Calls.Add($"Heading3:{text}");
    public void Paragraph(string text) => Calls.Add($"Paragraph:{text}");
    public void ParagraphItalic(string text) => Calls.Add($"ParagraphItalic:{text}");
    public void ParagraphIndented(string text) => Calls.Add($"ParagraphIndented:{text}");
    public void ParagraphItalicIndented(string text) => Calls.Add($"ParagraphItalicIndented:{text}");
    public void ParagraphFirstLine(string firstLine, string continuation, bool isItalic = false)
        => Calls.Add($"ParagraphFirstLine:{firstLine}");
    public void HeadingInline(string heading, string text) => Calls.Add($"HeadingInline:{heading}|{text}");
    public void WriteTextWithAttachmentLinks(string text) => Calls.Add($"WriteTextWithAttachmentLinks:{text}");
    public void ApplyPendingLinks() => Calls.Add("ApplyPendingLinks");
    public Dictionary<int, int> GetAttachmentPageNumbers() => new();
    public int GetCurrentPageNumber() => 1;
    public void Blank(double points = 8) => Calls.Add($"Blank:{points}");
    public void WriteAttachmentTocEntry(int pageNumber, int attachmentNumber, string fileName)
        => Calls.Add($"WriteAttachmentTocEntry:{attachmentNumber}:{fileName}");
    public int AddPdfAttachmentStart() { Calls.Add("AddPdfAttachmentStart"); return 1; }
    public void AddPdfAttachment(byte[] pdfContent, string fileName, int number)
        => Calls.Add($"AddPdfAttachment:{number}:{fileName}");
    public void AddImageAttachment(byte[] imageContent, string fileName, int number)
        => Calls.Add($"AddImageAttachment:{number}:{fileName}");
    public byte[] ToBytes() => ResultBytes;
}

public sealed class CapturingPdfWriterFactory : ISimplePdfWriterFactory
{
    public CapturingPdfWriter? Last { get; private set; }

    public ISimplePdfWriter Create()
    {
        Last = new CapturingPdfWriter();
        return Last;
    }
}
