using Microsoft.EntityFrameworkCore;
using StServer.Application.Interfaces;
using StServer.Domain.Entities;
using StServer.Infrastructure.Data;

namespace StServer.Infrastructure.Repositories;

public class MaterialRepository : IMaterialRepository
{
    private readonly AppDbContext _db;

    public MaterialRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Material>> GetAllAsync(Guid userId)
    {
        return await _db.Materials
            .Include(m => m.MaterialTags)
            .ThenInclude(mt => mt.Tag)
            .Include(mr => mr.Assessments)
            .Where(x => x.UserId == userId)
            .ToListAsync();
    }

    public async Task<Material?> GetByIdAsync(Guid materialId, Guid userId)
    {
        var material = await _db.Materials
            .Include(m => m.MaterialTags)
            .ThenInclude(mt => mt.Tag)
            .Include(mr => mr.Assessments)
            .FirstOrDefaultAsync(x => x.Id == materialId && x.UserId == userId);

        if (material is null)
        {
            return null;
        }
        
        return material;
    }

    public async Task AddAsync(Material material)
    {
        await _db.Materials.AddAsync(material);
    }

    public async Task<bool> DeleteAsync(Guid materialId, Guid userId)
    {
        var material = await _db.Materials
            .FirstOrDefaultAsync(x => x.Id == materialId && x.UserId == userId);
        
        if (material is null)
        {
            return false;
        }
        
        _db.Materials.Remove(material);
        return true;
        
    }

    public Task<List<Attempt>> GetAttemptsAsync(Guid materialId, Guid userId)
    {
        var attempts = _db.Attempts
            .Include(m => m.Results)
            .Where(x => x.Assessment.MaterialId == materialId && x.UserId == userId)
            .ToListAsync();

        return attempts;
    }

    public Task SaveChangesAsync()
    {
        return _db.SaveChangesAsync();
    }
}