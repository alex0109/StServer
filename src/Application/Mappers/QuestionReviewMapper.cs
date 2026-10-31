using Application.DTOs.Question;
using Application.Schedulers;
using Domain.Entities;

namespace Application.Mappers;

public static class QuestionReviewMapper
{
    public static QuestionProgress ToProgressEntity(Guid questionId, Guid userId, FlashcardCard card, DateTime now)
    {
        var progress = new QuestionProgress
        {
            QuestionId = questionId,
            UserId = userId,
            CreatedAt = now,
        };

        FlashcardMapper.ApplyCard(progress, card, now);

        return progress;
    }

    public static QuestionProgressDto ToProgressDto(QuestionProgress progress, int leechLapses) => new()
    {
        State = progress.State,
        Due = progress.Due,
        Reps = progress.Reps,
        Lapses = progress.Lapses,
        IsLeech = progress.Lapses >= leechLapses,
    };
}
