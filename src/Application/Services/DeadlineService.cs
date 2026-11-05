using Application.DTOs.Statistics;
using Application.Exceptions;
using Application.Interfaces;
using Application.Statistics;

namespace Application.Services;

public class DeadlineService : IDeadlineService
{
    private const int MaxYearsAhead = 5;
    private const int DaysShownAfterPassing = 7;

    private readonly IMaterialRepository _materials;
    private readonly IStatisticsRepository _statistics;
    private readonly IUserContext _user;
    private readonly IUserTimeZoneProvider _timeZones;
    private readonly TimeProvider _time;

    public DeadlineService(
        IMaterialRepository materials,
        IStatisticsRepository statistics,
        IUserContext user,
        IUserTimeZoneProvider timeZones,
        TimeProvider time)
    {
        _materials = materials;
        _statistics = statistics;
        _user = user;
        _timeZones = timeZones;
        _time = time;
    }

    public async Task<bool> SetAsync(Guid materialId, DateOnly? deadline)
    {
        var material = await _materials.GetByIdAsync(materialId, _user.UserId);
        if (material is null) return false;

        if (deadline is { } date)
        {
            var timeZone = await _timeZones.GetAsync(_user.UserId);
            var today = UserTimeZone.LocalDate(_time.GetUtcNow().UtcDateTime, timeZone);

            // The user's own today: for someone far from UTC the server's date can be off by one
            if (date < today || date > today.AddYears(MaxYearsAhead))
                throw new BadRequestException("The deadline must be between today and five years from now");
        }

        material.Deadline = deadline;
        await _materials.SaveChangesAsync();

        return true;
    }

    public async Task<DeadlinePlanDto?> GetPlanAsync(Guid materialId)
    {
        var plans = await BuildAsync(materialId);

        return plans.FirstOrDefault();
    }

    public async Task<List<DeadlinePlanDto>> GetAllAsync()
    {
        var plans = await BuildAsync(null);

        return plans
            .Where(x => x.DaysLeft >= -DaysShownAfterPassing)
            .OrderBy(x => x.DaysLeft)
            .ThenBy(x => x.Title)
            .ToList();
    }

    private async Task<List<DeadlinePlanDto>> BuildAsync(Guid? materialId)
    {
        var materials = await _statistics.GetDeadlineMaterialsAsync(_user.UserId, materialId);
        if (materials.Count == 0) return [];

        var items = (await _statistics.GetDeadlineItemsAsync(_user.UserId, materials.Select(x => x.MaterialId).ToList()))
            .ToLookup(x => x.MaterialId);
        var timeZone = await _timeZones.GetAsync(_user.UserId);
        var now = _time.GetUtcNow().UtcDateTime;

        return materials
            .Select(x => DeadlinePlanner.Build(x, items[x.MaterialId].ToList(), now, timeZone))
            .ToList();
    }
}
