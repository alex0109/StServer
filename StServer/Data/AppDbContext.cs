using StServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace StServer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        
    }

    public DbSet<Material> Materials => Set<Material>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Material>()
            .Property(x => x.Description)
            .HasColumnType("jsonb");
    }
}