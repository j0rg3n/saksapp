namespace SaksAppWeb.Services;

public interface ISimplePdfWriter
{
    void Title(string text);
    void Heading(string text);
    void Heading2(string text);
    void Heading3(string text);
    void Paragraph(string text);
    void ParagraphItalic(string text);
    void ParagraphIndented(string text);
    void ParagraphItalicIndented(string text);
    void ParagraphFirstLine(string firstLine, string continuation, bool isItalic = false);
    void HeadingInline(string heading, string text);
    void WriteTextWithAttachmentLinks(string text);
    void ApplyPendingLinks();
    Dictionary<int, int> GetAttachmentPageNumbers();
    int GetCurrentPageNumber();
    void Blank(double points = 8);
    void WriteAttachmentTocEntry(int pageNumber, int attachmentNumber, string fileName);
    int AddPdfAttachmentStart();
    void AddPdfAttachment(byte[] pdfContent, string fileName, int number);
    void AddImageAttachment(byte[] imageContent, string fileName, int number);
    byte[] ToBytes();
}

public interface ISimplePdfWriterFactory
{
    ISimplePdfWriter Create();
}

public sealed class SimplePdfWriterFactory : ISimplePdfWriterFactory
{
    public ISimplePdfWriter Create() => new SimplePdfWriter();
}
