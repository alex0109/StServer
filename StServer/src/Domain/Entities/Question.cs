using System.ComponentModel.DataAnnotations;

namespace StServer.Domain.Entities;

public class Question
{
    public Guid Id { get; set; }
    public ICollection<Result> Results { get; set; } = new List<Result>();
    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    [MaxLength(70)]
    public required string Title { get; set; }
    [MaxLength(1000)] 
    public required string Answer { get; set; }
    public required int Difficulty { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}