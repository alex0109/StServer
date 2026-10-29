using Application.DTOs.Statistics;
using Domain.Entities;

namespace Application.Interfaces;

public interface IStatisticsRepository
{
    Task<int> GetMaterialsCountAsync(Guid userId);
    Task<int> GetQuestionsCountAsync(Guid userId);

    // Results carry only score fields (no answer texts), read without tracking
    Task<List<Attempt>> GetFinishedAttemptsWithResultsAsync(
        Guid userId,
        DateTime? from = null,
        DateTime? to = null);

    // Difficulty, correctness, time and confidence of every answer of the user
    Task<List<AnswerStatRow>> GetAnswerStatRowsAsync(Guid userId);

    // Answers and correct answers per tag of the answered question's material
    Task<List<TagAnswerStat>> GetTagAnswerStatsAsync(
        Guid userId,
        DateTime? from = null,
        DateTime? toExclusive = null);

    // Moments of study: finished attempts and flashcard reviews, in UTC
    Task<List<DateTime>> GetActivityTimesAsync(Guid userId, DateTime? from = null);

    Task<List<DictionaryDueDto>> GetDueWordsAsync(Guid userId, DateTime dueBeforeUtc);

    Task<int> GetReviewsCountAsync(Guid userId, DateTime from, DateTime to);
}
