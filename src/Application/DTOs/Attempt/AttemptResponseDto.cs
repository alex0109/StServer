using StServer.Application.DTOs.Result;
using StServer.Domain.Utility.Attempt;

namespace StServer.Application.DTOs.Attempt;

public class AttemptResponseDto
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public AttemptStatus AttemptStatus  { get; set; }
    public double Score { get; set; }
    public int CorrectAnswers { get; set; }
    public int WrongAnswers { get; set; }
    public double TotalTimeSeconds { get; set; }
    public List<ResultResponseDto> Results { get; set; } = [];
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}