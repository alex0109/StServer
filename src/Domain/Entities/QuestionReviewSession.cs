namespace Domain.Entities;

// Questions reviewed from the "Today" queue, across materials. Not an attempt: it has no score
// and does not count in attempt statistics, every answer is only a spaced repetition review
public class QuestionReviewSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int QuestionsTotal { get; set; }
    public ICollection<QuestionReview> Reviews { get; set; } = new List<QuestionReview>();
    public required DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
