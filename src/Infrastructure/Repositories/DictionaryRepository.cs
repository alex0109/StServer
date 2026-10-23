using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class DictionaryRepository : IDictionaryRepository
{
    private readonly AppDbContext _context;

    public DictionaryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Dictionary>> GetAllByMaterialIdAsync(Guid materialId, Guid userId)
    {
        return await _context.Dictionaries
            .Where(d => d.MaterialId == materialId && d.UserId == userId)
            .Include(d => d.Entries)
                .ThenInclude(e => e.Progress)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<Dictionary?> GetByIdAsync(Guid dictionaryId, Guid userId)
    {
        return await _context.Dictionaries
            .Include(d => d.Entries)
                .ThenInclude(e => e.Progress)
            .FirstOrDefaultAsync(d => d.Id == dictionaryId && d.UserId == userId);
    }

    public async Task AddAsync(Dictionary dictionary)
    {
        await _context.Dictionaries.AddAsync(dictionary);
    }

    public async Task<bool> DeleteAsync(Guid dictionaryId, Guid userId)
    {
        var deleted = await _context.Dictionaries
            .Where(d => d.Id == dictionaryId && d.UserId == userId)
            .ExecuteDeleteAsync();

        return deleted > 0;
    }

    public async Task<DictionaryEntry?> GetEntryByIdAsync(Guid entryId, Guid userId)
    {
        return await _context.DictionaryEntries
            .Include(e => e.Progress)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.Dictionary.UserId == userId);
    }

    public async Task<List<DictionaryEntry>> GetEntriesByDictionaryIdAsync(Guid dictionaryId, Guid userId)
    {
        return await _context.DictionaryEntries
            .Where(e => e.DictionaryId == dictionaryId && e.Dictionary.UserId == userId)
            .Include(e => e.Question)
                .ThenInclude(q => q!.Options)
            .Include(e => e.Progress)
            .OrderBy(e => e.Position)
            .ToListAsync();
    }

    public async Task AddEntryAsync(DictionaryEntry entry)
    {
        await _context.DictionaryEntries.AddAsync(entry);
    }

    public async Task<bool> DeleteEntryAsync(Guid entryId, Guid userId)
    {
        var deleted = await _context.DictionaryEntries
            .Where(e => e.Id == entryId && e.Dictionary.UserId == userId)
            .ExecuteDeleteAsync();

        return deleted > 0;
    }

    public async Task<int> GetMaxEntryPositionAsync(Guid dictionaryId)
    {
        return await _context.DictionaryEntries
            .Where(e => e.DictionaryId == dictionaryId)
            .MaxAsync(e => (int?)e.Position) ?? -1;
    }

    public async Task<int> CountByMaterialIdAsync(Guid materialId, Guid userId)
    {
        return await _context.Dictionaries
            .CountAsync(d => d.MaterialId == materialId && d.UserId == userId);
    }

    public async Task<int> CountEntriesAsync(Guid dictionaryId)
    {
        return await _context.DictionaryEntries
            .CountAsync(e => e.DictionaryId == dictionaryId);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}