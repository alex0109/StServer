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
    public DbSet<MaterialFile> MaterialFiles => Set<MaterialFile>();
    
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
        
        modelBuilder.Entity<Tag>()
            .Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");
        
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

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Note_TypeConsistency",
                """
                ("Type" = 0 AND "DrawingContent" IS NULL AND "FileId" IS NULL) OR
                ("Type" = 1 AND "TextContent" IS NULL AND "FileId" IS NULL)
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
        
        modelBuilder.Entity<MaterialFile>()
            .HasOne(x => x.Material)
            .WithMany(m => m.MaterialFiles)
            .HasForeignKey(x => x.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);
        
    }
}