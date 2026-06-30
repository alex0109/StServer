using StServer.Application.DTOs.Tag;

namespace StServer.Application.Interfaces;

public interface ITagService
{
    Task<List<TagResponseDto>> GetAllTagsAsync();

    Task<TagResponseDto?> GetByIdAsync(Guid tagId);

    Task<TagResponseDto> CreateAsync(TagCreateDto dto);
    
    Task<TagResponseDto?> UpdateTagAsync(Guid tagId, TagUpdateDto tagDto);
    
    Task<bool> DeleteTagAsync(Guid tagId);
}