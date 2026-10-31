using Application.DTOs.Limits;
using Application.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class LimitsRepository : ILimitsRepository
{
    private readonly AppDbContext _db;

    public LimitsRepository(AppDbContext db)
    {
        _db = db;
    }

    // Same conditions as the counts in the services that check limits
    public async Task<LimitsUsageDto> GetUsageAsync(Guid userId)
    {
        var materials = await _db.Materials
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new MaterialLimitsUsageDto
            {
                MaterialId = m.Id,
                Title = m.Title,
                Questions = m.Questions.Count(q => q.IsActive && q.DeletedAt == null),
                Notes = m.Notes.Count(n => n.UserId == userId),
                Dictionaries = m.Dictionaries.Count(d => d.UserId == userId),
                MaxDictionaryEntries = m.Dictionaries
                    .Where(d => d.UserId == userId)
                    .Max(d => (int?)d.Entries.Count) ?? 0,
            })
            .ToListAsync();

        var tags = await _db.Tags.CountAsync(t => t.UserId == userId);

        var libraryCopies = await _db.Materials.CountAsync(m => m.UserId == userId && m.ImportedFromLibraryItemId != null);

        return new LimitsUsageDto
        {
            Materials = materials.Count,
            LibraryCopies = libraryCopies,
            Tags = tags,
            MaterialsUsage = materials,
        };
    }
}
