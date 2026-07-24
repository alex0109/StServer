using StServer.Domain.Entities;

namespace StServer.Application.Interfaces;

public interface IAttemptRepository
{
    Task<List<Attempt>?> GetFinishedAttemptsAsync(Guid materialId, Guid userId);
    Task<List<Attempt>?> GetAbandonedAttemptsAsync();
    Task<Attempt?> GetAttemptByIdAsync(Guid id, Guid userId);
    Task<Attempt?> GetAttemptWithAssessmentByIdAsync(Guid id, Guid userId);
    Task<Attempt?> GetAttemptWithResultsByIdAsync(Guid id, Guid userId);
    Task<Attempt?> GetFullAttemptByIdAsync(Guid id, Guid userId);
    Task AddAttemptAsync(Attempt attempt);
    Task AddResultAsync(Result result);
    Task SaveChangesAsync();
}