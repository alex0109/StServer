using StServer.Application.DTOs.Attempt;
using StServer.Application.DTOs.Question;
using StServer.Application.DTOs.Result;

namespace StServer.Application.Interfaces;

public interface IAttemptService
{
    Task<AttemptResponseDto?> GetAttempt(Guid id);
    Task<Guid?> StartAttempt(Guid assessmentId);
    Task<AnswerEvaluationResult> AnswerQuestion(Guid id, ResultRequestDto resultRequestDto);
    Task<AttemptResponseDto?> FinishAttempt(Guid id);
    Task<AttemptResponseDto?> GetResults(Guid id);
}