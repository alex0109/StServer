using System.ComponentModel.DataAnnotations;
using StServer.Domain.Utility.Question;

namespace StServer.Domain.Entities;

public class Question
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ICollection<Result> Results { get; set; } = new List<Result>();
    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    [MaxLength(70)]
    public required string Title { get; set; }
    [MaxLength(1000)] 
    public string? Answer { get; set; }
    public QuestionType QuestionType  { get; set; }
    public Guid? CorrectOptionId { get; set; }
    public ICollection<Option> Options { get; set; } = new List<Option>();
    public QuestionDifficulty QuestionDifficulty { get; set; }
    public string? Explanation { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}