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

    public async Task<List<Material>> GetAllAsync()
    {
        return await _db.Materials.ToListAsync();
    }

    public async Task<Material?> GetByIdAsync(Guid id)
    {
        var material = await _db.Materials.FirstOrDefaultAsync(x => x.Id == id);

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

    public async Task<bool> DeleteAsync(Guid id)
    {
        if (await _db.Materials.FindAsync(id) is Material material)
        {
            _db.Materials.Remove(material);
            return true;
        }
        
        return false;
    }

    public Task SaveChangesAsync()
    {
        return _db.SaveChangesAsync();
    }
}