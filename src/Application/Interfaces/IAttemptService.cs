using Application.DTOs.Attempt;
using Application.DTOs.Question;
using Application.DTOs.Result;
using Domain.Entities;

namespace Application.Interfaces;

public interface IAttemptService
{
    Task<List<AttemptResponseDto>?> GetFinishedAttempts(Guid materialId);
    Task<AttemptResponseDto?> GetAttempt(Guid id);
    Task<Guid?> StartAttempt(Guid assessmentId);
    Task<AnswerEvaluationResult> AnswerQuestion(Guid id, ResultRequestDto resultRequestDto);
    Task<bool> FinishAttempt(Guid id);
    Task MarkAbandonedAttempts();
    Task<AttemptResponseDto?> GetResults(Guid id);
}