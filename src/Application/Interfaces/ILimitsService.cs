using Application.DTOs.Limits;

namespace Application.Interfaces;

public interface ILimitsService
{
    Task<LimitsUsageDto> GetUsageAsync();
}
