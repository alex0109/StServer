using Application.DTOs.Review;
using Domain.Entities;

namespace Application.Interfaces;

public interface IReviewRepository
{
    Task<int> CountOverdueWordsAsync(Guid userId, DateTime dueBeforeUtc);

    // Per material: active questions due by the given time
    Task<List<MaterialDueDto>> GetDueQuestionsAsync(Guid userId, DateTime dueByUtc);

    Task<int> CountOverdueQuestionsAsync(Guid userId, DateTime dueBeforeUtc);

    // Active due questions with options and their material, most overdue first
    Task<List<Question>> GetDueQuestionsForSessionAsync(Guid userId, DateTime dueByUtc, int take);

    // One of the user's own active questions, with options and progress
    Task<Question?> GetQuestionAsync(Guid questionId, Guid userId);

    Task<QuestionReviewSession?> GetSessionAsync(Guid sessionId, Guid userId);
    Task<bool> IsAnsweredAsync(Guid sessionId, Guid questionId);
    Task AddSessionAsync(QuestionReviewSession session);
    Task SaveChangesAsync();
}
