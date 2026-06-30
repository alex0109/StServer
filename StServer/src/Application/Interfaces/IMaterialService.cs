using StServer.Application.DTOs.Attempt;
using StServer.Application.DTOs.Material;
using StServer.Application.DTOs.Tag;

namespace StServer.Application.Interfaces;

public interface IMaterialService
{
    Task<List<MaterialResponseDto>> GetAllAsync();

    Task<MaterialResponseDto?> GetByIdAsync(Guid materialId);

    Task<MaterialResponseDto> CreateAsync(MaterialCreateDto dto);

    Task<MaterialResponseDto?> UpdateAsync(Guid materialId, MaterialUpdateDto dto);

    Task<bool> DeleteAsync(Guid materialId);

    Task<MaterialStatisticsDto> GetStatisticsAsync();
    
    Task<List<AttemptResponseDto>> GetAttempts(Guid materialId);

    Task SyncMaterialTags(Guid materialId, List<Guid> tagIds);

}