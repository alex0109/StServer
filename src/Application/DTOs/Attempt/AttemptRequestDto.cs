using StServer.Application.DTOs.Result;

namespace StServer.Application.DTOs.Attempt;

public class AttemptRequestDto
{
    public List<ResultRequestDto> Answers { get; set; } = [];
}