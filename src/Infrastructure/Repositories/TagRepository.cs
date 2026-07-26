using Microsoft.EntityFrameworkCore;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;

namespace Infrastructure.Repositories;

public class TagRepository : ITagRepository
{
    private readonly AppDbContext _db;

    public TagRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Tag>?> GetAllTags(Guid userId)
    {
        return await _db.Tags.Where(x => x.UserId == userId).ToListAsync();
    }
    
    public async Task<List<Material>?> GetMaterialsByTagAsync(Guid tagId, Guid userId)
    {
        return await _db.Materials
            .Include(m => m.MaterialTags)
            .ThenInclude(mt => mt.Tag)
            .Where(m =>
                m.UserId == userId &&
                m.MaterialTags.Any(mt => mt.TagId == tagId))
            .ToListAsync();
    }

    public async Task<Tag?> GetTagByIdAsync(Guid id, Guid userId)
    {
        return await _db.Tags.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
    }

    public async Task<Tag> AddTagAsync(Tag tag)
    {
        await _db.Tags.AddAsync(tag);

        return tag;
    }

    public async Task<bool> DeleteTagAsync(Guid id, Guid userId)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (tag is null)
        {
            return false;
        }

        _db.Tags.Remove(tag);
        return true;
    }
    
    public async Task<int> CountByUserIdAsync(Guid userId)
    {
        return await _db.Tags
            .CountAsync(x => x.UserId == userId);
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}