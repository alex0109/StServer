using Application.DTOs.Option;
using Domain.Utility.Question;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Question;

public class QuestionResponseDto
{
    public Guid Id { get; set; }
    public Guid MaterialId { get; set; }
    [MaxLength(100)]
    public required string Title { get; set; }
    [MaxLength(1000)]
    public string? Answer { get; set; }
    public QuestionType QuestionType  { get; set; }
    public Guid? CorrectOptionId { get; set; }
    public List<OptionResponseDto>? Options { get; set; }
    public QuestionDifficulty QuestionDifficulty { get; set; }
    [MaxLength(1000)]
    public string? Explanation { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}