namespace Application.DTOs.Material;

public class MaterialDeadlineDto
{
    // Null removes the deadline
    public DateOnly? Deadline { get; set; }
}
