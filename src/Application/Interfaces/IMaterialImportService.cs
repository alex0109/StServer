using Application.DTOs.Material;

namespace Application.Interfaces;

public interface IMaterialImportService
{
    Task<MaterialImportResultDto> ImportAsync(MaterialImportDto dto, CancellationToken cancellationToken = default);
}
