using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Jobs;

// Keeps question repetition complete: reviews for attempts finished before it existed,
// and questions moved to the configured algorithm after switching it
public class QuestionReviewJob
{
    private readonly IQuestionReviewService _reviews;
    private readonly ILogger<QuestionReviewJob> _logger;

    public QuestionReviewJob(IQuestionReviewService reviews, ILogger<QuestionReviewJob> logger)
    {
        _reviews = reviews;
        _logger = logger;
    }

    public async Task Run()
    {
        var added = await _reviews.BackfillAsync();
        var rebuilt = await _reviews.RebuildOutdatedAsync();

        if (added > 0 || rebuilt > 0)
            _logger.LogInformation("Question reviews: {Added} added from past attempts, {Rebuilt} rebuilt", added, rebuilt);
    }
}
