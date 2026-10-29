using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class FeedbackLogRepository : IFeedbackLogRepository
{
    private readonly AppDbContext _db;

    public FeedbackLogRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<int> CountSinceAsync(string senderKey, DateTime since) =>
        _db.FeedbackSubmissions.CountAsync(x => x.SenderKey == senderKey && x.CreatedAt >= since);

    public async Task AddAsync(FeedbackSubmission submission)
    {
        await _db.FeedbackSubmissions.AddAsync(submission);
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();

    public Task<int> DeleteCreatedBeforeAsync(DateTime createdBefore) =>
        _db.FeedbackSubmissions.Where(x => x.CreatedAt < createdBefore).ExecuteDeleteAsync();
}
