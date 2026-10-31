using Application.Profile;

namespace Application.Interfaces;

public interface ILearningSettingsProvider
{
    Task<LearningSettings> GetAsync(Guid userId);

    // Counts reviews between the given bounds of the user's local day
    Task<DailyBudget> GetBudgetAsync(Guid userId, DateTime dayStartUtc, DateTime dayEndUtc);
}
