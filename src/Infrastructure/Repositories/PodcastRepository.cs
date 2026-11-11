using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class PodcastRepository : IPodcastRepository
{
    private readonly AppDbContext _db;

    public PodcastRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<MaterialPodcast?> GetByMaterialAsync(Guid materialId, Guid userId) =>
        _db.MaterialPodcasts.FirstOrDefaultAsync(x => x.MaterialId == materialId && x.UserId == userId);

    public Task<MaterialPodcast?> GetByIdAsync(Guid podcastId) =>
        _db.MaterialPodcasts.FirstOrDefaultAsync(x => x.Id == podcastId);

    public Task<List<string>> GetAudioPathsAsync(Guid userId) =>
        _db.MaterialPodcasts
            .Where(x => x.UserId == userId && x.AudioPath != null)
            .Select(x => x.AudioPath!)
            .ToListAsync();

    public async Task AddAsync(MaterialPodcast podcast) => await _db.MaterialPodcasts.AddAsync(podcast);

    public void Remove(MaterialPodcast podcast) => _db.MaterialPodcasts.Remove(podcast);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
