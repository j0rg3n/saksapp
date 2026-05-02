using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using SaksAppWeb.Models;

namespace SaksAppWeb.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        if (!optionsBuilder.IsConfigured) return;

        optionsBuilder.AddInterceptors(new BusyTimeoutInterceptor());
    }

    public DbSet<BoardCase> BoardCases => Set<BoardCase>();
    public DbSet<CaseComment> CaseComments => Set<CaseComment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<CaseCommentAttachment> CaseCommentAttachments => Set<CaseCommentAttachment>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingCase> MeetingCases => Set<MeetingCase>();

    public DbSet<PdfGeneration> PdfGenerations => Set<PdfGeneration>();

    public DbSet<MeetingMinutes> MeetingMinutes => Set<MeetingMinutes>();
    public DbSet<MeetingMinutesCaseEntry> MeetingMinutesCaseEntries => Set<MeetingMinutesCaseEntry>();
    public DbSet<MeetingMinutesAttachment> MeetingMinutesAttachments => Set<MeetingMinutesAttachment>();
    public DbSet<MeetingMinutesCaseEntryAttachment> MeetingMinutesCaseEntryAttachments => Set<MeetingMinutesCaseEntryAttachment>();

    public DbSet<MeetingCaseAttachment> MeetingCaseAttachments => Set<MeetingCaseAttachment>();

    public DbSet<CaseEvent> CaseEvents => Set<CaseEvent>();
    public DbSet<CaseEventCase> CaseEventCases => Set<CaseEventCase>();
    public DbSet<CaseEventAttachment> CaseEventAttachments => Set<CaseEventAttachment>();
    public DbSet<MeetingEventLink> MeetingEventLinks => Set<MeetingEventLink>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<BoardCase>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<CaseComment>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<Attachment>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<CaseCommentAttachment>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<Meeting>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<MeetingCase>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<PdfGeneration>().HasQueryFilter(x => !x.Meeting.IsDeleted);
        builder.Entity<MeetingMinutes>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<MeetingMinutesCaseEntry>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<MeetingMinutesAttachment>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<MeetingMinutesCaseEntryAttachment>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<MeetingCaseAttachment>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<CaseEvent>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<CaseEventCase>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<CaseEventAttachment>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<MeetingEventLink>().HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<PdfGeneration>(b =>
        {
            b.HasIndex(x => new { x.MeetingId, x.DocumentType, x.SequenceNumber }).IsUnique();
            b.HasIndex(x => new { x.MeetingId, x.DocumentType });

            b.HasOne(x => x.Meeting)
                .WithMany()
                .HasForeignKey(x => x.MeetingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MeetingMinutes>(b =>
        {
            b.HasIndex(x => x.MeetingId).IsUnique();

            b.HasOne(x => x.Meeting)
                .WithMany()
                .HasForeignKey(x => x.MeetingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MeetingMinutesCaseEntry>(b =>
        {
            b.HasIndex(x => new { x.MeetingId, x.MeetingCaseId }).IsUnique();

            b.HasOne(x => x.Meeting)
                .WithMany()
                .HasForeignKey(x => x.MeetingId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.MeetingCase)
                .WithMany()
                .HasForeignKey(x => x.MeetingCaseId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.BoardCase)
                .WithMany()
                .HasForeignKey(x => x.BoardCaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MeetingMinutesAttachment>(b =>
        {
            b.HasIndex(x => new { x.MeetingId, x.AttachmentId }).IsUnique();

            b.HasOne(x => x.Meeting)
                .WithMany()
                .HasForeignKey(x => x.MeetingId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Attachment)
                .WithMany()
                .HasForeignKey(x => x.AttachmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MeetingMinutesCaseEntryAttachment>(b =>
        {
            b.HasIndex(x => new { x.MeetingMinutesCaseEntryId, x.AttachmentId }).IsUnique();

            b.HasOne(x => x.MeetingMinutesCaseEntry)
                .WithMany()
                .HasForeignKey(x => x.MeetingMinutesCaseEntryId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Attachment)
                .WithMany()
                .HasForeignKey(x => x.AttachmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MeetingCaseAttachment>(b =>
        {
            b.HasIndex(x => new { x.MeetingCaseId, x.AttachmentId }).IsUnique();

            b.HasOne(x => x.MeetingCase)
                .WithMany()
                .HasForeignKey(x => x.MeetingCaseId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Attachment)
                .WithMany()
                .HasForeignKey(x => x.AttachmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseEventCase>(b =>
        {
            b.HasIndex(x => new { x.CaseEventId, x.BoardCaseId }).IsUnique().HasFilter("\"IsDeleted\" = 0");

            b.HasOne(x => x.CaseEvent)
                .WithMany(x => x.Cases)
                .HasForeignKey(x => x.CaseEventId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.BoardCase)
                .WithMany()
                .HasForeignKey(x => x.BoardCaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseEventAttachment>(b =>
        {
            b.HasIndex(x => new { x.CaseEventId, x.AttachmentId }).IsUnique();

            b.HasOne(x => x.CaseEvent)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.CaseEventId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Attachment)
                .WithMany()
                .HasForeignKey(x => x.AttachmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MeetingEventLink>(b =>
        {
            b.HasIndex(x => new { x.MeetingId, x.CaseEventId }).IsUnique();

            b.HasOne(x => x.Meeting)
                .WithMany()
                .HasForeignKey(x => x.MeetingId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.CaseEvent)
                .WithOne(x => x.MeetingLink)
                .HasForeignKey<MeetingEventLink>(x => x.CaseEventId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
