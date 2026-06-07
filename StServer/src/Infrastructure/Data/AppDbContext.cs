using Microsoft.EntityFrameworkCore;
using StServer.Domain.Entities;

namespace StServer.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        
    }

    public DbSet<Material> Materials => Set<Material>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<Result> Results => Set<Result>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Material>()
            .Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");
        
        modelBuilder.Entity<Material>()
            .Property(x => x.Description)
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
        
        modelBuilder.Entity<Result>()
            .Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");
        
        modelBuilder.Entity<Result>()
            .HasOne(a => a.Assessment)
            .WithMany(m => m.Results)
            .HasForeignKey(a => a.AssessmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}