using System.ComponentModel.DataAnnotations;

namespace StServer.Domain.Entities;

public class Option
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    [MaxLength(500)]
    public required string Name { get; set; }
}