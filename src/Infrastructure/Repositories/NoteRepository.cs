using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class NoteRepository : INoteRepository
{
    private readonly AppDbContext _db;

    public NoteRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Note>> GetAllAsync(Guid materialId, Guid userId)
    {
        return await _db.Notes
            .Where(x => x.MaterialId == materialId && x.UserId == userId).ToListAsync();
    }

    public async Task<Note?> GetByIdAsync(Guid noteId, Guid materialId, Guid userId)
    {
        var note = await _db.Notes
            .FirstOrDefaultAsync(x => x.Id == noteId && x.MaterialId == materialId && x.UserId == userId);

        if (note is null)
        {
            return null;
        }
        
        return note;
    }

    public async Task AddAsync(Note note)
    {
        await _db.Notes.AddAsync(note);
    }

    public async Task<bool> DeleteAsync(Guid noteId, Guid materialId, Guid userId)
    {
        var note = await _db.Notes
            .FirstOrDefaultAsync(x => x.Id == noteId && x.MaterialId == materialId && x.UserId == userId);
        
        if (note is null)
        {
            return false;
        }
        
        _db.Notes.Remove(note);
        return true;
        
    }
    
    public async Task<int> CountByUserIdAsync(Guid materialId, Guid userId)
    {
        return await _db.Notes
            .CountAsync(x => x.MaterialId == materialId && x.UserId == userId);
    }

    public Task SaveChangesAsync()
    {
        return _db.SaveChangesAsync();
    }
}