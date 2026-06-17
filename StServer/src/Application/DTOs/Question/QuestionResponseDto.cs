using StServer.Domain.Utility.Question;

namespace StServer.Application.DTOs.Question;

public class QuestionResponseDto
{
    public Guid Id { get; set; }
    public Guid MaterialId { get; set; }
    public required string Title { get; set; }
    public string? Answer { get; set; }
    public QuestionType QuestionType  { get; set; }
    public Guid? CorrectOptionId { get; set; }
    public List<OptionDto>? Options { get; set; }
    public QuestionDifficulty QuestionDifficulty { get; set; }
    public string? Explanation { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}