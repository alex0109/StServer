using Application.DTOs.Attempt;
using Application.DTOs.Question;
using Application.DTOs.Result;
using Domain.Entities;

namespace Application.Interfaces;

public interface IAttemptService
{
    // Empty when nothing is finished yet: no attempts is a normal state, not a missing resource
    Task<List<AttemptResponseDto>> GetFinishedAttempts(Guid materialId);
    Task<AttemptResponseDto?> GetAttempt(Guid id);
    // useExamSettings: the material's settings (levels, number of questions, timer) shape the attempt;
    // without them it has every active question and no timer
    Task<Guid?> StartAttempt(Guid assessmentId, bool useExamSettings = false);
    // The attempt's questions in the order they are shown, choices without the right answers
    Task<List<QuestionReducedDto>?> GetQuestions(Guid attemptId);
    Task<AnswerEvaluationResult> AnswerQuestion(Guid id, ResultRequestDto resultRequestDto);
    Task<bool> FinishAttempt(Guid id);
    Task MarkAbandonedAttempts();
    Task<AttemptResponseDto?> GetResults(Guid id);

    // The user says a rejected open answer is right; returns the updated results
    Task<AttemptResponseDto?> AcceptAnswerAsync(Guid attemptId, Guid resultId);

    // Takes the acceptance back, possible for a short time after it
    Task<AttemptResponseDto?> UndoAcceptAsync(Guid attemptId, Guid resultId);
}