using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Domain.Utility.Material;

namespace Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        
    }

    public DbSet<Material> Materials => Set<Material>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<MaterialTag> MaterialTags => Set<MaterialTag>();
    public DbSet<Option> Options => Set<Option>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Dictionary> Dictionaries => Set<Dictionary>();
    public DbSet<DictionaryEntry> DictionaryEntries => Set<DictionaryEntry>();
    public DbSet<DictionaryEntryProgress> DictionaryEntryProgresses => Set<DictionaryEntryProgress>();
    public DbSet<FlashcardSession> FlashcardSessions => Set<FlashcardSession>();
    public DbSet<FlashcardReview> FlashcardReviews => Set<FlashcardReview>();
    public DbSet<QuestionProgress> QuestionProgresses => Set<QuestionProgress>();
    public DbSet<QuestionReview> QuestionReviews => Set<QuestionReview>();
    public DbSet<QuestionReviewSession> QuestionReviewSessions => Set<QuestionReviewSession>();
    public DbSet<MaterialFile> MaterialFiles => Set<MaterialFile>();
    public DbSet<MaterialShare> MaterialShares => Set<MaterialShare>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<SubscriptionEvent> SubscriptionEvents => Set<SubscriptionEvent>();
    public DbSet<AiUsage> AiUsages => Set<AiUsage>();
    public DbSet<AiUsageMonthly> AiUsageMonthly => Set<AiUsageMonthly>();
    public DbSet<UserNotificationSettings> UserNotificationSettings => Set<UserNotificationSettings>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<FeedbackSubmission> FeedbackSubmissions => Set<FeedbackSubmission>();
    public DbSet<LibraryItem> LibraryItems => Set<LibraryItem>();
    public DbSet<LibraryRequest> LibraryRequests => Set<LibraryRequest>();
    public DbSet<AdminAuditEntry> AdminAuditEntries => Set<AdminAuditEntry>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Material>()
            .Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");
        
        modelBuilder.Entity<Material>()
            .Property(x => x.Content)
            .HasColumnType("jsonb");
        
        modelBuilder.Entity<Question>()
            .Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");
        
        modelBuilder.Entity<Question>()
            .HasOne(a => a.Material)
            .WithMany(m => m.Questions)
            .HasForeignKey(a => a.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<Question>()
            .HasOne(a => a.Note)
            .WithMany(m => m.Questions)
            .HasForeignKey(a => a.NoteId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<Question>()
            .HasOne(q => q.DictionaryEntry)
            .WithOne(e => e.Question)
            .HasForeignKey<Question>(q => q.DictionaryEntryId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<Question>().HasIndex(q => q.NoteId);

        // Question count per user (overview, limits)
        modelBuilder.Entity<Question>().HasIndex(q => q.UserId);

        // Purge of soft-deleted questions only looks at deleted rows
        modelBuilder.Entity<Question>()
            .HasIndex(q => q.DeletedAt, "IX_Questions_DeletedAt")
            .HasFilter("\"DeletedAt\" IS NOT NULL");
        
        modelBuilder.Entity<Assessment>()
            .Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");
        
        modelBuilder.Entity<Assessment>()
            .HasOne(a => a.Material)
            .WithMany(m => m.Assessments)
            .HasForeignKey(a => a.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<Attempt>()
            .Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");
        
        modelBuilder.Entity<Attempt>()
            .HasOne(a => a.Assessment)
            .WithMany(m => m.Attempts)
            .HasForeignKey(a => a.AssessmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Statistics and streak: finished attempts of a user by finish time
        modelBuilder.Entity<Attempt>()
            .HasIndex(a => new { a.UserId, a.AttemptStatus, a.FinishedAt });

        // Cleanup of abandoned attempts
        modelBuilder.Entity<Attempt>()
            .HasIndex(a => new { a.AttemptStatus, a.StartedAt });
        
        modelBuilder.Entity<Result>()
            .Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");
        
        modelBuilder.Entity<Result>()
            .HasOne(a => a.Attempt)
            .WithMany(m => m.Results)
            .HasForeignKey(a => a.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<Result>()
            .HasOne(r => r.Question)
            .WithMany(q => q.Results)
            .HasForeignKey(r => r.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Statistics: all answers of a user, optionally in a time range
        modelBuilder.Entity<Result>()
            .HasIndex(r => new { r.UserId, r.AnsweredAt });
        
        modelBuilder.Entity<Tag>()
            .Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        modelBuilder.Entity<Tag>()
            .HasIndex(x => x.UserId);
        
        modelBuilder.Entity<MaterialTag>()
            .HasKey(mt => new { mt.MaterialId, mt.TagId });
        
        modelBuilder.Entity<MaterialTag>()
            .HasOne(mt => mt.Material)
            .WithMany(m => m.MaterialTags)
            .HasForeignKey(mt => mt.MaterialId);
        
        modelBuilder.Entity<MaterialTag>()
            .HasOne(mt => mt.Tag)
            .WithMany(t => t.MaterialTags)
            .HasForeignKey(mt => mt.TagId);
        
        modelBuilder.Entity<Option>()
            .Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");
        
        modelBuilder.Entity<Option>()
            .HasOne(o => o.Question)
            .WithMany(q => q.Options)
            .HasForeignKey(o => o.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<Note>()
            .Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");
        
        modelBuilder.Entity<Note>()
            .Property(x => x.DrawingContent)
            .HasColumnType("jsonb");
        
        modelBuilder.Entity<Note>()
            .HasOne(a => a.Material)
            .WithMany(m => m.Notes)
            .HasForeignKey(a => a.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasOne(x => x.File)
                .WithMany()
                .HasForeignKey(x => x.FileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.MaterialId);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => new { x.MaterialId, x.UserId });

            // Summary is a text note, and a material has at most one
            entity.HasIndex(x => x.MaterialId, "IX_Notes_SingleSummary")
                .IsUnique()
                .HasFilter("\"Type\" = 3");

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Note_TypeConsistency",
                """
                ("Type" = 0 AND "DrawingContent" IS NULL AND "FileId" IS NULL) OR
                ("Type" = 1 AND "TextContent" IS NULL AND "FileId" IS NULL) OR
                ("Type" = 3 AND "DrawingContent" IS NULL AND "FileId" IS NULL)
                """
            ));
        });
        
        modelBuilder.Entity<Dictionary>(entity =>
        {
            entity.Property(x => x.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(d => d.Material)
                .WithMany(m => m.Dictionaries)
                .HasForeignKey(d => d.MaterialId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.MaterialId);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => new { x.MaterialId, x.UserId });
        });

        modelBuilder.Entity<DictionaryEntry>(entity =>
        {
            entity.Property(x => x.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(e => e.Dictionary)
                .WithMany(d => d.Entries)
                .HasForeignKey(e => e.DictionaryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.DictionaryId, x.Position });
        });

        modelBuilder.Entity<DictionaryEntryProgress>(entity =>
        {
            entity.HasKey(x => x.EntryId);

            entity.HasOne(p => p.Entry)
                .WithOne(e => e.Progress)
                .HasForeignKey<DictionaryEntryProgress>(p => p.EntryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.UserId, x.Due });
        });

        modelBuilder.Entity<QuestionProgress>(entity =>
        {
            entity.HasKey(x => x.QuestionId);

            entity.HasOne(p => p.Question)
                .WithOne(q => q.Progress)
                .HasForeignKey<QuestionProgress>(p => p.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // The review queue: what is due for a user
            entity.HasIndex(x => new { x.UserId, x.Due });
        });

        modelBuilder.Entity<QuestionReview>(entity =>
        {
            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            entity.HasOne(r => r.Question)
                .WithMany(q => q.Reviews)
                .HasForeignKey(r => r.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.QuestionId, x.ReviewedAt });

            // One review per question per attempt: finishing an attempt twice schedules nothing new
            entity.HasIndex(x => new { x.AttemptId, x.QuestionId })
                .IsUnique()
                .HasFilter("\"AttemptId\" IS NOT NULL");

            // The log outlives its session, a question is answered once per session
            entity.HasOne(r => r.Session)
                .WithMany(s => s.Reviews)
                .HasForeignKey(r => r.SessionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => new { x.SessionId, x.QuestionId })
                .IsUnique()
                .HasFilter("\"SessionId\" IS NOT NULL");
        });

        modelBuilder.Entity<QuestionReviewSession>(entity =>
        {
            entity.Property(x => x.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            entity.HasIndex(x => new { x.UserId, x.StartedAt });
        });

        modelBuilder.Entity<FlashcardSession>(entity =>
        {
            entity.Property(x => x.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(s => s.Dictionary)
                .WithMany(d => d.FlashcardSessions)
                .HasForeignKey(s => s.DictionaryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.DictionaryId, x.UserId });

            // Cleanup of sessions that were never finished
            entity.HasIndex(x => x.StartedAt, "IX_FlashcardSessions_Unfinished")
                .HasFilter("\"FinishedAt\" IS NULL");
        });

        modelBuilder.Entity<FlashcardReview>(entity =>
        {
            // Id comes from the client, so a retried request does not create a duplicate
            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            entity.HasOne(r => r.Session)
                .WithMany(s => s.Reviews)
                .HasForeignKey(r => r.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Entry)
                .WithMany(e => e.Reviews)
                .HasForeignKey(r => r.EntryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.EntryId, x.ReviewedAt });
            entity.HasIndex(x => new { x.UserId, x.ReviewedAt });
        });
        
        modelBuilder.Entity<MaterialShare>(entity =>
        {
            entity.Property(x => x.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(s => s.Material)
                .WithMany(m => m.Shares)
                .HasForeignKey(s => s.MaterialId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.Token).IsUnique();
            entity.HasIndex(x => new { x.MaterialId, x.UserId });
        });

        modelBuilder.Entity<Material>()
            .HasIndex(x => new { x.UserId, x.ImportedFromMaterialId });

        modelBuilder.Entity<UserSubscription>(entity =>
        {
            entity.HasKey(x => x.UserId);

            // A provider's ids point at exactly one user
            entity.HasIndex(x => new { x.Provider, x.ProviderSubscriptionId })
                .IsUnique()
                .HasFilter("\"ProviderSubscriptionId\" IS NOT NULL");
            entity.HasIndex(x => new { x.Provider, x.ProviderCustomerId })
                .IsUnique()
                .HasFilter("\"ProviderCustomerId\" IS NOT NULL");
        });

        modelBuilder.Entity<SubscriptionEvent>(entity =>
        {
            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            entity.Property(x => x.Payload)
                .HasColumnType("jsonb");

            // Repeated deliveries of one event are stored once
            entity.HasIndex(x => new { x.Provider, x.ProviderEventId })
                .IsUnique();
            entity.HasIndex(x => x.ReceivedAt);
            entity.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<AiUsage>(entity =>
        {
            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            // Quota count: user, feature and month
            entity.HasIndex(x => new { x.UserId, x.Feature, x.CreatedAt });
            entity.HasIndex(x => new { x.Status, x.CreatedAt });
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<AiUsageMonthly>(entity =>
        {
            entity.Property(x => x.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            entity.HasIndex(x => new { x.UserId, x.Year, x.Month, x.Feature, x.Provider, x.Model })
                .IsUnique();
        });

        modelBuilder.Entity<UserNotificationSettings>(entity =>
        {
            entity.HasKey(x => x.UserId);
        });

        // No FK to the settings: either row can exist first
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(x => x.UserId);
        });

        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            entity.Property(x => x.Data)
                .HasColumnType("jsonb");

            // A reminder of one kind is made once per day or week, even if the job runs twice
            entity.HasIndex(x => new { x.UserId, x.Type, x.PeriodKey })
                .IsUnique();

            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
        });

        modelBuilder.Entity<LibraryItem>(entity =>
        {
            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            entity.Property(x => x.Snapshot)
                .HasColumnType("jsonb");

            // The catalogue: newest published first
            entity.HasIndex(x => new { x.Status, x.PublishedAt });
            entity.HasIndex(x => x.AuthorUserId);
        });

        modelBuilder.Entity<LibraryRequest>(entity =>
        {
            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            entity.Property(x => x.Snapshot)
                .HasColumnType("jsonb");

            // Review queue, the author's own list, and "is something about this already waiting"
            entity.HasIndex(x => new { x.Status, x.CreatedAt });
            entity.HasIndex(x => new { x.UserId, x.Status });
            entity.HasIndex(x => new { x.LibraryItemId, x.Status });
            entity.HasIndex(x => new { x.MaterialId, x.Status });
        });

        // "Already imported" for library cards
        modelBuilder.Entity<Material>()
            .HasIndex(x => new { x.UserId, x.ImportedFromLibraryItemId });

        modelBuilder.Entity<AdminAuditEntry>(entity =>
        {
            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<FeedbackSubmission>(entity =>
        {
            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            // Monthly count per sender
            entity.HasIndex(x => new { x.SenderKey, x.CreatedAt });
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<MaterialFile>()
            .HasOne(x => x.Material)
            .WithMany(m => m.MaterialFiles)
            .HasForeignKey(x => x.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);
        
    }
}