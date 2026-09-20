using Application.DTOs.Note;

namespace Application.Interfaces;

public interface INoteService
{
    Task<List<NoteResponseDto>> GetAllAsync(Guid materialId);

    Task<NoteResponseDto?> GetByIdAsync(Guid noteId, Guid materialId);

    Task<NoteResponseDto> CreateAsync(NoteCreateDto dto, Guid materialId);

    Task<NoteResponseDto?> UpdateAsync(Guid noteId, Guid materialId, NoteUpdateDto dto);

    Task<bool> DeleteAsync(Guid noteId, Guid materialId);
}