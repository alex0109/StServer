using Application.DTOs.Limits;

namespace Application.Interfaces;

public interface ILimitsRepository
{
    Task<LimitsUsageDto> GetUsageAsync(Guid userId);
}
