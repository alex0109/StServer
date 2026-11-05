using Application.DTOs.Statistics;

namespace Application.Interfaces;

public interface IDeadlineService
{
    // Null removes the deadline. False when there is no such material
    Task<bool> SetAsync(Guid materialId, DateOnly? deadline);

    // Null when the material has no deadline
    Task<DeadlinePlanDto?> GetPlanAsync(Guid materialId);

    // Nearest first, those that passed a week ago are gone
    Task<List<DeadlinePlanDto>> GetAllAsync();
}
