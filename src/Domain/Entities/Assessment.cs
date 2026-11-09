using Domain.Utility.Question;

namespace Domain.Entities;

public class Assessment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ICollection<Attempt> Attempts { get; set; } = new List<Attempt>();
    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    // Optional time limit for an attempt on the whole material
    public int? TimerMinutes { get; set; }
    // Difficulty levels an attempt on the whole material draws from; empty means all of them
    public List<QuestionDifficulty> Difficulties { get; set; } = [];
    // How many questions such an attempt takes at most; null means every one that matches
    public int? QuestionCount { get; set; }
}
