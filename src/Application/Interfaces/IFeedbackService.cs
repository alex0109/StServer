using Application.DTOs.Feedback;

namespace Application.Interfaces;

public interface IFeedbackService
{
    Task SendAsync(FeedbackCreateDto dto, CancellationToken cancellationToken = default);
}
