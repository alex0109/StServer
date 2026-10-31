using Microsoft.EntityFrameworkCore;
using Application.DTOs.Material;
using Application.DTOs.Note;
using Application.DTOs.Tag;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;

namespace Infrastructure.Repositories;

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
            .Include(ml => ml.Notes.OrderBy(n => n.CreatedAt).ThenBy(n => n.Id))
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Id)
            .AsSplitQuery()
            .ToListAsync();
    }

    // The sidebar list: everything except the rich text of materials and the content of notes,
    // which are the heavy part and are loaded only for the material or note that is opened.
    // Same order as the full list: materials by id, notes oldest first
    public async Task<List<MaterialResponseDto>> GetListAsync(Guid userId)
    {
        return await _db.Materials
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Id)
            .Select(m => new MaterialResponseDto
            {
                Id = m.Id,
                AssessmentId = m.Assessments.Select(a => (Guid?)a.Id).FirstOrDefault(),
                Title = m.Title,
                Type = m.Type,
                MaterialTags = m.MaterialTags
                    .OrderBy(mt => mt.TagId)
                    .Select(mt => new TagResponseDto
                    {
                        Id = mt.Tag.Id,
                        Name = mt.Tag.Name,
                        Color = mt.Tag.Color,
                    })
                    .ToList(),
                Notes = m.Notes
                    .OrderBy(n => n.CreatedAt)
                    .ThenBy(n => n.Id)
                    .Select(n => new NoteResponseDto
                    {
                        Id = n.Id,
                        MaterialId = n.MaterialId,
                        Title = n.Title,
                        Type = n.Type,
                        Order = n.Order,
                        CreatedAt = n.CreatedAt,
                        UpdatedAt = n.UpdatedAt,
                    })
                    .ToList(),
                Link = m.Link,
                Status = m.Status,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
                Version = m.Version,
                IsActive = m.IsActive,
                IsImported = m.ImportedFromMaterialId != null || m.ImportedFromLibraryItemId != null,
                IsFromLibrary = m.ImportedFromLibraryItemId != null,
                ImportedAt = m.ImportedAt,
            })
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<Material?> GetByIdAsync(Guid materialId, Guid userId)
    {
        var material = await _db.Materials
            .Include(m => m.MaterialTags)
            .ThenInclude(mt => mt.Tag)
            .Include(mr => mr.Assessments)
            .Include(ml => ml.Notes.OrderBy(n => n.CreatedAt).ThenBy(n => n.Id))
            .AsSplitQuery()
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
    
    public async Task<int> CountOwnByUserIdAsync(Guid userId)
    {
        return await _db.Materials.CountAsync(m => m.UserId == userId && m.ImportedFromLibraryItemId == null);
    }

    public async Task<int> CountLibraryCopiesAsync(Guid userId)
    {
        return await _db.Materials.CountAsync(m => m.UserId == userId && m.ImportedFromLibraryItemId != null);
    }

    public async Task<int> CountByUserIdAsync(Guid userId)
    {
        return await _db.Materials.CountAsync(x => x.UserId == userId);
    }

    public Task SaveChangesAsync()
    {
        return _db.SaveChangesAsync();
    }
}