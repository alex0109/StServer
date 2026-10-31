using Application.DTOs.Material;
using Application.DTOs.Share;

namespace Application.Interfaces;

public interface IMaterialShareService
{
    Task<MaterialShareResponseDto?> GetShareAsync(Guid materialId);

    Task<MaterialShareResponseDto?> CreateShareAsync(Guid materialId);

    Task<bool> RevokeShareAsync(Guid materialId);

    Task<SharedMaterialPreviewDto?> GetPreviewAsync(string token);

    Task<MaterialResponseDto?> ImportAsync(string token);
}
