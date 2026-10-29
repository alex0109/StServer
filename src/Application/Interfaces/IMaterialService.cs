using Application.DTOs.Material;

namespace Application.Interfaces;

public interface IMaterialService
{
    Task<List<MaterialResponseDto>> GetAllAsync();
    // Without material and note content, for lists
    Task<List<MaterialResponseDto>> GetListAsync();

    Task<MaterialResponseDto?> GetByIdAsync(Guid materialId);

    Task<MaterialResponseDto> CreateAsync(MaterialCreateDto dto);

    Task<MaterialResponseDto?> UpdateAsync(Guid materialId, MaterialUpdateDto dto);

    Task<bool> DeleteAsync(Guid materialId);

    Task<MaterialStatisticsDto> GetStatisticsAsync();
    
    Task<MaterialResponseDto?> AddTagToMaterialAsync(Guid materialId, Guid tagId);
    
    Task<MaterialResponseDto?> DeleteTagFromMaterialAsync(Guid materialId, Guid tagId);

}