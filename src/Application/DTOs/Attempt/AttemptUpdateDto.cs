using StServer.Domain.Utility.Attempt;

namespace StServer.Application.DTOs.Attempt;

public class AttemptUpdateDto
{
    public AttemptStatus? AttemptStatus  { get; set; }
}