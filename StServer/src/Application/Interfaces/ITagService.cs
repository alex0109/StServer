using StServer.Application.DTOs.Material;
using StServer.Application.DTOs.Tag;
using StServer.Domain.Entities;

namespace StServer.Application.Interfaces;

public interface ITagService
{
    Task<List<TagResponseDto>?> GetAllTagsAsync();
    
    Task<List<MaterialResponseDto>?> GetMaterialsByTagAsync(Guid tagId);

    Task<TagResponseDto?> GetByIdAsync(Guid tagId);

    Task<TagResponseDto> CreateAsync(TagCreateDto dto);
    
    Task<TagResponseDto?> UpdateTagAsync(Guid tagId, TagUpdateDto tagDto);
    
    Task<bool> DeleteTagAsync(Guid tagId);
}