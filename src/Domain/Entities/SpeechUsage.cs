using System.ComponentModel.DataAnnotations;

namespace Domain.Entities;

// One recording of a podcast's voices: the cost log of the speech service. Kept apart from
// the podcast, so a replaced or deleted podcast still counts in the costs
public class SpeechUsage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    // No link to the podcast row: it can be replaced or deleted, the cost stays
    public Guid PodcastId { get; set; }

    [MaxLength(10)]
    public required string Language { get; set; }

    // Spoken characters sent to the service, what it bills by. A failed recording still
    // counts the parts that were voiced before it stopped
    public int Characters { get; set; }
    public bool Succeeded { get; set; }

    public required DateTime CreatedAt { get; set; }
}
