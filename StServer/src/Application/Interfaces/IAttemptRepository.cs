using StServer.Domain.Entities;

namespace StServer.Application.Interfaces;

public interface IAttemptRepository
{
    Task<Attempt?> GetAttemptByIdAsync(Guid id, Guid userId);
    Task AddAsync(Attempt attempt);
    Task SaveChangesAsync();
}