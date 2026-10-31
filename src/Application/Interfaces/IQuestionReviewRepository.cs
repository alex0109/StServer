using Domain.Entities;
using Domain.Utility.Flashcard;

namespace Application.Interfaces;

public interface IQuestionReviewRepository
{
    // Questions with their FSRS state, only the user's own
    Task<List<Question>> GetQuestionsWithProgressAsync(IReadOnlyCollection<Guid> questionIds, Guid userId);

    // Questions already reviewed from this attempt
    Task<HashSet<Guid>> GetReviewedQuestionIdsAsync(Guid attemptId);

    Task<List<QuestionReview>> GetReviewsForQuestionsAsync(IReadOnlyCollection<Guid> questionIds);

    Task<QuestionReview?> GetAttemptReviewAsync(Guid attemptId, Guid questionId);

    // Answers in finished attempts that have no review yet, oldest first
    Task<List<Result>> GetUnreviewedFinishedResultsAsync(int take);

    Task<List<Question>> GetQuestionsWithOutdatedProgressAsync(FlashcardAlgorithm currentAlgorithm, int take);

    Task AddReviewAsync(QuestionReview review);
    Task AddProgressAsync(QuestionProgress progress);
    void RemoveProgress(QuestionProgress progress);
    Task SaveChangesAsync();

    // Questions of the user that have a review state
    Task<List<Guid>> GetQuestionIdsWithProgressAsync(Guid userId);
}
