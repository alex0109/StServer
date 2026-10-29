using Domain.Entities;

namespace Application.Interfaces;

public interface IFeedbackLogRepository
{
    Task<int> CountSinceAsync(string senderKey, DateTime since);

    Task AddAsync(FeedbackSubmission submission);

    Task SaveChangesAsync();

    Task<int> DeleteCreatedBeforeAsync(DateTime createdBefore);
}
