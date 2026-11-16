using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class MaterialVideoRepository : IMaterialVideoRepository
{
    private readonly AppDbContext _db;

    public MaterialVideoRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<MaterialVideo?> GetByMaterialAsync(Guid materialId, Guid userId) =>
        _db.MaterialVideos.FirstOrDefaultAsync(x => x.MaterialId == materialId && x.UserId == userId);

    public async Task AddAsync(MaterialVideo video) => await _db.MaterialVideos.AddAsync(video);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
