using StServer.Application.DTOs.Attempt;
using StServer.Application.DTOs.Question;
using StServer.Application.DTOs.Result;
using StServer.Domain.Entities;

namespace StServer.Application.Interfaces;

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