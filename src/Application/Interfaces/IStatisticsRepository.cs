using Domain.Entities;

namespace Application.Interfaces;

public interface IStatisticsRepository
{
    Task<int> GetMaterialsCountAsync(Guid userId);
    Task<int> GetQuestionsCountAsync(Guid userId);

    Task<List<Attempt>> GetFinishedAttemptsWithResultsAsync(
        Guid userId,
        DateTime? from = null,
        DateTime? to = null);

    Task<List<Result>> GetResultsWithQuestionAndTagsAsync(
        Guid userId,
        DateTime? from = null,
        DateTime? to = null);
}
