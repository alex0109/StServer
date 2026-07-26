using Application.DTOs.Result;

namespace Application.DTOs.Attempt;

public class AttemptRequestDto
{
    public List<ResultRequestDto> Answers { get; set; } = [];
}