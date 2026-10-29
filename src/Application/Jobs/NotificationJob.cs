using Application.Interfaces;

namespace Application.Jobs;

public class NotificationJob
{
    private readonly INotificationGenerator _generator;

    public NotificationJob(INotificationGenerator generator)
    {
        _generator = generator;
    }

    // Hourly: every user gets reminders at their own local hours
    public async Task Run()
    {
        await _generator.RunAsync();
    }
}
