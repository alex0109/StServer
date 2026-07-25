namespace Domain.Entities;

public class Assessment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ICollection<Attempt> Attempts { get; set; } = new List<Attempt>();
    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;
}