using Application.DTOs.Flashcard;
using Application.DTOs.Result;
using Application.DTOs.Review;

namespace Application.Interfaces;

// The "Today" queue: words from every dictionary and questions from every material
public interface IReviewService
{
    Task<ReviewTodayDto> GetTodayAsync();

    Task<FlashcardSessionResponseDto> StartWordsAsync(FlashcardSessionCreateDto dto);

    Task<QuestionReviewSessionDto> StartQuestionsAsync(QuestionReviewSessionCreateDto dto);

    Task<ReviewAnswerResultDto?> AnswerQuestionAsync(Guid sessionId, ResultRequestDto dto);

    Task<bool> FinishQuestionsAsync(Guid sessionId);
}
