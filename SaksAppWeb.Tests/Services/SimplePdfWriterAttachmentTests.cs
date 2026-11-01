using Microsoft.Extensions.Logging;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using SaksAppWeb.Services;
using Xunit;

namespace SaksAppWeb.Tests.Services;

public class SimplePdfWriterAttachmentTests
{
    static SimplePdfWriterAttachmentTests() => FontResolverBootstrap.Initialize();

    /// <summary>Builds a standalone PDF of the requested page count, to embed as an attachment.</summary>
    private static byte[] MakePdf(int pageCount)
    {
        var doc = new PdfDocument();
        for (var i = 0; i < pageCount; i++) doc.AddPage();
        using var ms = new MemoryStream();
        doc.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    /// <summary>
    /// The regression this guards: the TOC used to be written with GetCurrentPageNumber() + 1 for
    /// every entry, so all attachments showed the same page. Start pages must be distinct and
    /// ascending, and must account for each attachment's cover page plus its own page count.
    /// </summary>
    [Fact]
    public void AttachmentPageNumbers_AreDistinctAndAccountForEachAttachmentLength()
    {
        var writer = new SimplePdfWriter();
        writer.Title("Innkalling");
        writer.Heading2("Vedlegg");

        writer.WriteAttachmentTocEntryDeferred(1, "1.1", "a.pdf");
        writer.WriteAttachmentTocEntryDeferred(2, "2.1", "b.pdf");
        writer.WriteAttachmentTocEntryDeferred(3, "3.1", "c.pdf");

        var pagesBeforeAttachments = writer.GetCurrentPageNumber();

        writer.AddPdfAttachment(MakePdf(3), "a.pdf", 1, "1.1");
        writer.AddPdfAttachment(MakePdf(1), "b.pdf", 2, "2.1");
        writer.AddPdfAttachment(MakePdf(2), "c.pdf", 3, "3.1");

        writer.ApplyAttachmentTocPageNumbers();

        var pages = writer.GetAttachmentPageNumbers();

        // Each attachment contributes one cover page plus its own pages.
        Assert.Equal(pagesBeforeAttachments + 1, pages[1]);
        Assert.Equal(pagesBeforeAttachments + 1 + 1 + 3, pages[2]);
        Assert.Equal(pagesBeforeAttachments + 1 + 1 + 3 + 1 + 1, pages[3]);

        Assert.Equal(3, pages.Values.Distinct().Count());
        Assert.True(pages[1] < pages[2] && pages[2] < pages[3], "start pages must ascend");
    }

    /// <summary>
    /// The TOC hyperlinks must point at the same pages the TOC prints. They previously all targeted
    /// one guessed page, and were additionally shifted a page early by an off-by-one.
    /// </summary>
    [Fact]
    public void TocHyperlinks_TargetTheSamePagesAsThePrintedNumbers()
    {
        var writer = new SimplePdfWriter();
        writer.Title("Innkalling");
        writer.WriteAttachmentTocEntryDeferred(1, "1.1", "a.pdf");
        writer.WriteAttachmentTocEntryDeferred(2, "1.2", "b.pdf");
        writer.WriteAttachmentTocEntryDeferred(3, "2.1", "c.pdf");
        writer.AddPdfAttachment(MakePdf(3), "a.pdf", 1, "1.1");
        writer.AddPdfAttachment(MakePdf(1), "b.pdf", 2, "2.1");
        writer.AddPdfAttachment(MakePdf(2), "c.pdf", 3, "3.1");
        writer.ApplyAttachmentTocPageNumbers();

        var expected = writer.GetAttachmentPageNumbers().Values.OrderBy(x => x).ToArray();
        var bytes = writer.ToBytes();

        using var ms = new MemoryStream(bytes);
        var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Modify);
        var toc = doc.Pages[0];

        Assert.Equal(3, toc.Annotations.Count);

        var actual = new List<int>();
        for (var i = 0; i < toc.Annotations.Count; i++)
        {
            var dest = toc.Annotations[i].Elements.GetArray("/Dest");
            Assert.NotNull(dest);
            var pageRef = dest!.Elements[0] as PdfSharpCore.Pdf.Advanced.PdfReference;
            Assert.NotNull(pageRef);

            for (var p = 0; p < doc.PageCount; p++)
            {
                if (ReferenceEquals(doc.Pages[p].Reference, pageRef))
                {
                    actual.Add(p + 1);
                    break;
                }
            }
        }

        Assert.Equal(expected, actual.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void ProducesAReadablePdf_AfterApplyingTocPageNumbers()
    {
        var writer = new SimplePdfWriter();
        writer.Title("Innkalling");
        writer.HistoryEntry("01.09.2026", "Avvik", "#DC3545", "Eksempel resten av teksten");
        writer.HistoryEntry("01.09.2026", null, null, "Vanlig hendelse");
        writer.WriteAttachmentTocEntryDeferred(1, "1.1", "a.pdf");
        writer.AddPdfAttachment(MakePdf(2), "a.pdf", 1, "1.1");
        writer.ApplyAttachmentTocPageNumbers();

        var bytes = writer.ToBytes();

        using var ms = new MemoryStream(bytes);
        var reopened = PdfReader.Open(ms, PdfDocumentOpenMode.ReadOnly);
        Assert.True(reopened.PageCount >= 3);
    }

    private static byte[] MakePasswordProtectedPdf()
    {
        var doc = new PdfDocument();
        doc.AddPage();
        doc.SecuritySettings.UserPassword = "user";
        doc.SecuritySettings.OwnerPassword = "owner";
        using var ms = new MemoryStream();
        doc.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    private static readonly byte[] Garbage = "this is not a pdf or an image"u8.ToArray();

    public static TheoryData<string> UnreadableAttachmentKinds => new() { "garbage-pdf", "encrypted-pdf", "garbage-image" };

    /// <summary>
    /// A corrupt or password-protected attachment must not take down the whole export. It keeps
    /// its cover page (so later page numbers stay right), contributes no pages of its own, and the
    /// failure is logged rather than printed.
    /// </summary>
    [Theory]
    [MemberData(nameof(UnreadableAttachmentKinds))]
    public void UnreadableAttachment_KeepsCoverPage_LogsWarning_AndDoesNotShiftLaterAttachments(string kind)
    {
        var logger = new CapturingLogger<SimplePdfWriter>();
        var writer = new SimplePdfWriter(logger);
        writer.Title("Referat");
        writer.WriteAttachmentTocEntryDeferred(1, "1.1", "bad");
        writer.WriteAttachmentTocEntryDeferred(2, "1.2", "good.pdf");

        var pagesBeforeAttachments = writer.GetCurrentPageNumber();

        switch (kind)
        {
            case "garbage-pdf": writer.AddPdfAttachment(Garbage, "bad.pdf", 1, "1.1"); break;
            case "encrypted-pdf": writer.AddPdfAttachment(MakePasswordProtectedPdf(), "bad.pdf", 1, "1.1"); break;
            case "garbage-image": writer.AddImageAttachment(Garbage, "bad.jpg", 1, "1.1"); break;
        }
        writer.AddPdfAttachment(MakePdf(2), "good.pdf", 2, "2.1");
        writer.ApplyAttachmentTocPageNumbers();

        var pages = writer.GetAttachmentPageNumbers();
        Assert.Equal(pagesBeforeAttachments + 1, pages[1]);
        Assert.Equal(pagesBeforeAttachments + 2, pages[2]); // bad attachment = cover page only

        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.NotNull(warning.Exception);
        Assert.Contains("bad.", warning.Message);

        using var ms = new MemoryStream(writer.ToBytes());
        var reopened = PdfReader.Open(ms, PdfDocumentOpenMode.ReadOnly);
        Assert.Equal(pagesBeforeAttachments + 1 + 1 + 2, reopened.PageCount);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception), exception));
    }

    [Theory]
    [InlineData("Tekst med lås 🔒.", "Tekst med lås.")]
    [InlineData("Tekst 👍🏽 videre", "Tekst videre")]
    [InlineData("Ferdig ✅", "Ferdig")]
    [InlineData("❤️ Tekst", " Tekst")]
    [InlineData("Ærlig å si: «øy» — 50 000,-", "Ærlig å si: «øy» — 50 000,-")]
    public void Clean_RemovesCharactersTheFontCannotDraw(string input, string expected) =>
        Assert.Equal(expected, SimplePdfWriter.Clean(input));

    /// <summary>
    /// Fills page 1 with single-line paragraphs until exactly one more fits, returning that count.
    /// </summary>
    private static int ParagraphsLeavingRoomForOneMore()
    {
        var probe = new SimplePdfWriter();
        var count = 0;
        while (probe.GetCurrentPageNumber() == 1)
        {
            probe.Paragraph("linje");
            count++;
        }
        // The last paragraph opened page 2, so count - 1 fit on page 1; leave room for one more.
        return count - 2;
    }

    /// <summary>
    /// A heading with room for itself but not for any of what follows must move to the next page
    /// (typography review M6), whereas a plain paragraph in the same spot stays.
    /// </summary>
    [Fact]
    public void Heading_IsKeptWithNext_WhileParagraphInSameSpotIsNot()
    {
        var fill = ParagraphsLeavingRoomForOneMore();

        var withParagraph = new SimplePdfWriter();
        for (var i = 0; i < fill; i++) withParagraph.Paragraph("linje");
        withParagraph.Paragraph("siste linje");
        Assert.Equal(1, withParagraph.GetCurrentPageNumber());

        var withHeading = new SimplePdfWriter();
        for (var i = 0; i < fill; i++) withHeading.Paragraph("linje");
        withHeading.Heading3("Til møtet:");
        Assert.Equal(2, withHeading.GetCurrentPageNumber());

        var withLabelLine = new SimplePdfWriter();
        for (var i = 0; i < fill; i++) withLabelLine.Paragraph("linje");
        withLabelLine.Paragraph("Etikett:");
        Assert.Equal(2, withLabelLine.GetCurrentPageNumber());
    }

    /// <summary>Edge cases for the wrapper: none may throw, and the result must be a valid PDF.</summary>
    [Fact]
    public void Wrapping_HandlesLongWordsListsEmojiAndEmptyText()
    {
        var writer = new SimplePdfWriter();
        writer.HistoryEntry("01.10.2026", "Tiltak", "#FFC107", new string('x', 400));
        writer.HistoryEntry("01.10.2026", null, null, "");
        writer.HistoryEntry("01.10.2026", "Avvik", "#DC3545", "Første\n\n1. punkt som er langt nok til å bryte over flere linjer i dokumentet, helt sikkert\n- strek");
        writer.Paragraph("🔒🔒🔒");
        writer.WriteAttachmentTocEntryDeferred(1, "1.1", new string('n', 300) + ".pdf");
        writer.AddPdfAttachment(MakePdf(1), new string('n', 300) + ".pdf", 1, "1.1");
        writer.ApplyAttachmentTocPageNumbers();

        using var ms = new MemoryStream(writer.ToBytes());
        Assert.True(PdfReader.Open(ms, PdfDocumentOpenMode.ReadOnly).PageCount >= 2);
    }
}

public class PdfSampleRenderer
{
    static PdfSampleRenderer() => FontResolverBootstrap.Initialize();

    private static byte[] MakePdf(int pageCount)
    {
        var doc = new PdfDocument();
        for (var i = 0; i < pageCount; i++) doc.AddPage();
        using var ms = new MemoryStream();
        doc.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    /// <summary>
    /// Renders a sample innkalling so PDF layout can be inspected by eye. Opt-in: set
    /// PDF_SAMPLE_DIR to an existing directory. Assertions cannot tell you whether a badge overlaps
    /// its text or a page number lands outside its gutter, so this exists to make that checkable.
    /// </summary>
    [Fact]
    public void RenderSampleInnkalling()
    {
        var outDir = Environment.GetEnvironmentVariable("PDF_SAMPLE_DIR");
        if (string.IsNullOrWhiteSpace(outDir) || !Directory.Exists(outDir)) return;

        var pdf = new SimplePdfWriter();
        // Synthetic placeholder content only — never real case text, names or file names.
        pdf.Title("Innkalling styremøte #1 2000 — 01.01.2000 kl. 18:00");
        pdf.Heading2("2. Eksempelsak (Person A; #1)");
        pdf.ParagraphItalic("Beskrivelse av eksempelsaken i kursiv.");
        pdf.HeadingInline("Status: ", "Statustekst for eksempelsaken.");
        pdf.HeadingInline("Tidsfrist: ", "01.02.2000");
        pdf.Heading3("Til møtet:");
        pdf.ParagraphIndented("Etikett:\n1. Første punkt i en nummerert liste, langt nok til at det brytes over mer enn én linje på siden.\n2. Andre punkt.\n- Punkt med strek.");
        pdf.Heading3("Historikk siden forrige møte:");
        pdf.HistoryEntry("01.01.2000", "Tiltak", "#FFC107",
            "Hendelsestekst med merke, lang nok til å fylle hele første linje og fortsette med hengende innrykk.");
        pdf.HistoryEntry("02.01.2000", "Avvik", "#DC3545", "Kort avvikstekst.");
        pdf.HistoryEntry("03.01.2000", "Tiltak", "#FFC107", "Tekst med emoji 🔒.");
        pdf.HistoryEntry("04.01.2000", null, null,
            "Vanlig hendelse uten merke, også denne lang nok til å brytes over to linjer i dokumentet.");

        var longName = new string('a', 60) + "_" + new string('b', 60) + " (1).pdf";
        pdf.Blank(6);
        pdf.Heading2("Vedlegg");
        pdf.WriteAttachmentTocEntryDeferred(1, "2.1", "vedlegg-a.pdf");
        pdf.WriteAttachmentTocEntryDeferred(2, "2.2", "vedlegg-b.pdf");
        pdf.WriteAttachmentTocEntryDeferred(3, "3.1", longName);
        pdf.AddPdfAttachment(MakePdf(3), "vedlegg-a.pdf", 1, "2.1");
        pdf.AddPdfAttachment(MakePdf(1), "vedlegg-b.pdf", 2, "2.2");
        pdf.AddPdfAttachment("garbage"u8.ToArray(), longName, 3, "3.1");
        pdf.ApplyAttachmentTocPageNumbers();

        File.WriteAllBytes(Path.Combine(outDir, "sample-innkalling.pdf"), pdf.ToBytes());
    }
}
