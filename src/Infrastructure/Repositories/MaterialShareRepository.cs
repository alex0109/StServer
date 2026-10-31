using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class MaterialShareRepository : IMaterialShareRepository
{
    private readonly AppDbContext _context;

    public MaterialShareRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<MaterialShare?> GetActiveByMaterialIdAsync(Guid materialId, Guid userId)
    {
        return await _context.MaterialShares
            .FirstOrDefaultAsync(s => s.MaterialId == materialId && s.UserId == userId && s.RevokedAt == null);
    }

    public async Task<MaterialShare?> GetActiveByTokenAsync(string token)
    {
        return await _context.MaterialShares
            .Include(s => s.Material)
                .ThenInclude(m => m.Notes)
            .Include(s => s.Material)
                .ThenInclude(m => m.Questions)
                    .ThenInclude(q => q.Options)
            .Include(s => s.Material)
                .ThenInclude(m => m.Dictionaries)
                    .ThenInclude(d => d.Entries)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Token == token && s.RevokedAt == null);
    }

    public async Task<bool> HasImportedAsync(Guid sourceMaterialId, Guid userId)
    {
        return await _context.Materials
            .AnyAsync(m => m.UserId == userId && m.ImportedFromMaterialId == sourceMaterialId);
    }

    public async Task AddAsync(MaterialShare share)
    {
        await _context.MaterialShares.AddAsync(share);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
