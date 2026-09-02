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

        writer.AddPdfAttachment(MakePdf(3), "a.pdf", 1);
        writer.AddPdfAttachment(MakePdf(1), "b.pdf", 2);
        writer.AddPdfAttachment(MakePdf(2), "c.pdf", 3);

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
        writer.AddPdfAttachment(MakePdf(3), "a.pdf", 1);
        writer.AddPdfAttachment(MakePdf(1), "b.pdf", 2);
        writer.AddPdfAttachment(MakePdf(2), "c.pdf", 3);
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
        writer.ParagraphFirstLineWithBadge("2026-09-01", "Avvik", "#DC3545", "Sirene", "resten av teksten", isItalic: true);
        writer.ParagraphFirstLineWithBadge("2026-09-01", null, null, "Vanlig hendelse", "", isItalic: true);
        writer.WriteAttachmentTocEntryDeferred(1, "1.1", "a.pdf");
        writer.AddPdfAttachment(MakePdf(2), "a.pdf", 1);
        writer.ApplyAttachmentTocPageNumbers();

        var bytes = writer.ToBytes();

        using var ms = new MemoryStream(bytes);
        var reopened = PdfReader.Open(ms, PdfDocumentOpenMode.ReadOnly);
        Assert.True(reopened.PageCount >= 3);
    }

    /// <summary>A badge must not be drawn for ordinary events, and must not throw when absent.</summary>
    [Fact]
    public void ParagraphFirstLineWithBadge_WithoutBadge_MatchesPlainParagraphLayout()
    {
        var withoutBadge = new SimplePdfWriter();
        withoutBadge.ParagraphFirstLineWithBadge("2026-09-01", null, null, "line", "continuation");

        var plain = new SimplePdfWriter();
        plain.ParagraphFirstLine("2026-09-01: line", "continuation");

        Assert.Equal(plain.GetCurrentPageNumber(), withoutBadge.GetCurrentPageNumber());
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
        pdf.Title("Innkalling styremøte #9 2026 — 29.09.2026 kl. 18:00");
        pdf.Heading2("1. Sporing av setningsskader (Jørgen; #19)");
        pdf.Heading3("Historikk siden forrige møte:");
        pdf.ParagraphFirstLineWithBadge("2026-09-01", "Tiltak", "#FFC107",
            "Befaring gjennomført med Ulf",
            "Teigen fra Murbyen Oslo i dag morges. Avgir notat om 1-2 uker.", isItalic: true);
        pdf.ParagraphFirstLineWithBadge("2026-09-01", "Avvik", "#DC3545",
            "Etter service melder Isak",
            "Flo Bødal om at kabler og feste/sirene er gulnet og lukter dårlig.", isItalic: true);
        pdf.ParagraphFirstLineWithBadge("2026-08-20", null, null,
            "Mottatt prisanslag fra WSP på 50000,-",
            "eks. mva. Avventer sammenligning med Consisu.", isItalic: true);

        pdf.Blank(6);
        pdf.Heading2("Vedlegg");
        pdf.WriteAttachmentTocEntryDeferred(1, "1.1", "befaringsnotat.pdf");
        pdf.WriteAttachmentTocEntryDeferred(2, "1.2", "prisanslag-wsp.pdf");
        pdf.WriteAttachmentTocEntryDeferred(3, "2.1", "vurderingsnotat-rib.pdf");
        pdf.AddPdfAttachment(MakePdf(3), "befaringsnotat.pdf", 1);
        pdf.AddPdfAttachment(MakePdf(1), "prisanslag-wsp.pdf", 2);
        pdf.AddPdfAttachment(MakePdf(2), "vurderingsnotat-rib.pdf", 3);
        pdf.ApplyAttachmentTocPageNumbers();

        File.WriteAllBytes(Path.Combine(outDir, "sample-innkalling.pdf"), pdf.ToBytes());
    }
}
