using StServer.Application.DTOs.Material;

namespace StServer.Application.Interfaces;

public interface IMaterialService
{
    Task<List<MaterialResponseDto>> GetAllAsync();

    Task<MaterialResponseDto?> GetByIdAsync(Guid id);

    Task<MaterialResponseDto> CreateAsync(MaterialCreateDto dto);

    Task<MaterialResponseDto?> UpdateAsync(Guid id, MaterialUpdateDto dto);

    Task<bool> DeleteAsync(Guid id);

    Task<MaterialStatisticsDto> GetStatisticsAsync();
}