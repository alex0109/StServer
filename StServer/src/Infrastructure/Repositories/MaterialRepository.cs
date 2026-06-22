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
        return await _db.Materials.Where(x => x.UserId == userId).ToListAsync();
    }

    public async Task<Material?> GetByIdAsync(Guid id, Guid userId)
    {
        var material = await _db.Materials
            .Include(m => m.MaterialTags)
            .ThenInclude(mt => mt.Tag)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (material is null)
        {
            return null;
        }
        
        return material;
    }

    public async Task<Material> AddAsync(Material material)
    {
        await _db.Materials.AddAsync(material);
        return material;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId)
    {
        var material = _db.Materials
            .FirstOrDefault(x => x.Id == id && x.UserId == userId);
        
        if (material is null)
        {
            return false;
        }
        
        _db.Materials.Remove(material);
        return true;
        
    }

    public Task<List<Attempt>> GetAttemptsAsync(Guid id, Guid userId)
    {
        var attemtps = _db.Attempts
            .Include(m => m.Results)
            .Where(x => x.Assessment.MaterialId == id && x.UserId == userId)
            .ToListAsync();

        return attemtps;
    }

    public Task SaveChangesAsync()
    {
        return _db.SaveChangesAsync();
    }
}