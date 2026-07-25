using Microsoft.EntityFrameworkCore;
using Domain.Entities;

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
    }
}