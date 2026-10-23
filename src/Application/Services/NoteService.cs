using Application.Constants;
using Application.DTOs.Note;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappers;

namespace Application.Services;

public class NoteService : INoteService
{
    private readonly INoteRepository _repo;
    private readonly IUserContext _user;
    private readonly ILimitLock _limitLock;
    

    public NoteService(
        INoteRepository repo,
        IUserContext user,
        ILimitLock limitLock)
    {
        _repo = repo;
        _user = user;
        _limitLock = limitLock;
    }

    public async Task<List<NoteResponseDto>> GetAllAsync(Guid materialId)
    {
        var notes = await _repo.GetAllAsync(materialId, _user.UserId);
        
        return notes.Select(NoteMapper.ToDto).ToList();
    }

    public async Task<NoteResponseDto?> GetByIdAsync(Guid noteId, Guid materialId)
    {
        var note = await _repo.GetByIdAsync(noteId, materialId, _user.UserId);

        if (note is null)
            return null;
        
        return NoteMapper.ToDto(note);
    }

    public async Task<NoteResponseDto?> CreateAsync(Guid materialId, NoteCreateDto dto)
    {
        if (!await _repo.MaterialExistsAsync(materialId, _user.UserId))
            return null;

        await using var limitLock = await _limitLock.AcquireAsync(LimitScope.Notes, materialId);

        var count = await _repo.CountByUserIdAsync(materialId, _user.UserId);

        if (count >= UserLimits.MaxNotesPerMaterial)
        {
            throw new LimitExceededException($"Notes limit reached: {UserLimits.MaxNotesPerMaterial} per material");
        }
        
        var note = NoteMapper.ToEntity(dto, materialId, _user.UserId);
        await _repo.AddAsync(note);
        
        await _repo.SaveChangesAsync();
        await limitLock.CommitAsync();
        
        return NoteMapper.ToDto(note);
    }

    public async Task<NoteResponseDto?> UpdateAsync(Guid noteId, Guid materialId, NoteUpdateDto dto)
    {
        var note = await _repo.GetByIdAsync(
            noteId,
            materialId,
            _user.UserId);

        if (note is null)
            return null;

        NoteMapper.ApplyUpdate(note, dto);

        await _repo.SaveChangesAsync();

        return NoteMapper.ToDto(note);
    }

    public async Task<bool> DeleteAsync(Guid noteId, Guid materialId)
    {
        var result = await _repo.DeleteAsync(noteId, materialId, _user.UserId);
        
        await _repo.SaveChangesAsync();

        return result;
    }
}