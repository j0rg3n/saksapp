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
    /// <summary>
    /// A history entry: "&lt;date&gt; [badge] &lt;text&gt;" in italic. The badge is drawn only when both
    /// label and colour are given. The text fills the rest of the first line, and further lines
    /// wrap with a hanging indent.
    /// </summary>
    void HistoryEntry(string date, string? badgeLabel, string? badgeColorHex, string text);
    void HeadingInline(string heading, string text);
    void WriteTextWithAttachmentLinks(string text);
    void ApplyPendingLinks();
    Dictionary<int, int> GetAttachmentPageNumbers();
    int GetCurrentPageNumber();
    void Blank(double points = 8);
    void WriteAttachmentTocEntry(int pageNumber, int attachmentNumber, string fileName);
    void WriteAttachmentTocEntryLabel(int pageNumber, string label, string fileName);
    void WriteAttachmentTocEntryDeferred(int attachmentNumber, string label, string fileName);
    void ApplyAttachmentTocPageNumbers();
    int AddPdfAttachmentStart();
    /// <param name="number">Internal sequence number, the key used by the attachment index.</param>
    /// <param name="label">The attachment's visible identifier (e.g. "2.1"), shown on its caption.</param>
    void AddPdfAttachment(byte[] pdfContent, string fileName, int number, string label);
    void AddImageAttachment(byte[] imageContent, string fileName, int number, string label);
    void OutcomeBadge(string label, string colorHex);
    byte[] ToBytes();
}

public interface ISimplePdfWriterFactory
{
    ISimplePdfWriter Create();
}

public sealed class SimplePdfWriterFactory(ILogger<SimplePdfWriter> logger) : ISimplePdfWriterFactory
{
    public ISimplePdfWriter Create() => new SimplePdfWriter(logger);
}
