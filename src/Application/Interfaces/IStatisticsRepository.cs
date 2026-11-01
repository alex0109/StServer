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
    // Active questions whose next review is due by the given time
    Task<int> CountDueQuestionsAsync(Guid userId, DateTime dueBeforeUtc);

    Task<int> GetReviewsCountAsync(Guid userId, DateTime from, DateTime to);

    // Every flashcard review of the user since the given moment, without the FSRS fields
    Task<List<FlashcardAnswerRow>> GetFlashcardAnswerRowsAsync(Guid userId, DateTime from);

    // Term and translation of the user's words; ids of deleted or foreign words are skipped
    Task<List<WordTerm>> GetWordTermsAsync(Guid userId, IReadOnlyCollection<Guid> entryIds);

    // Every review of a word or a question (attempt answers included), in UTC: the unit of the daily goal
    Task<List<DateTime>> GetReviewTimesAsync(Guid userId, DateTime? from = null);

    // Reviews in a span, and how many of them were words seen for the first time
    Task<(int Reviews, int NewWords)> CountReviewsAsync(Guid userId, DateTime from, DateTime to);
}
