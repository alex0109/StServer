using StServer.StServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace StServer.StServer.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        
    }

    public DbSet<Material> Materials => Set<Material>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Material>()
            .Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");
        
        modelBuilder.Entity<Material>()
            .Property(x => x.Description)
            .HasColumnType("jsonb");
        
        modelBuilder.Entity<Assessment>()
            .Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");
        
        modelBuilder.Entity<Assessment>()
            .HasOne(a => a.Material)
            .WithMany(m => m.Assessments)
            .HasForeignKey(a => a.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}