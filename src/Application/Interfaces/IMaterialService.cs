using Application.DTOs.Attempt;
using Application.DTOs.Material;
using Application.DTOs.Tag;

namespace Application.Interfaces;

public interface IMaterialService
{
    Task<List<MaterialResponseDto>> GetAllAsync();

    Task<MaterialResponseDto?> GetByIdAsync(Guid materialId);

    Task<MaterialResponseDto> CreateAsync(MaterialCreateDto dto);

    Task<MaterialResponseDto?> UpdateAsync(Guid materialId, MaterialUpdateDto dto);

    Task<bool> DeleteAsync(Guid materialId);

    Task<MaterialStatisticsDto> GetStatisticsAsync();
    
    Task<MaterialResponseDto?> AddTagToMaterialAsync(Guid materialId, Guid tagId);
    
    Task<MaterialResponseDto?> DeleteTagFromMaterialAsync(Guid materialId, Guid tagId);

}