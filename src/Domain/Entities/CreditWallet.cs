namespace Domain.Entities;

// Prepaid AI credits of one person; each person on a Duo has their own. The balance is the sum of
// CreditTransactions, kept here too so a request can lock and check one row
public class CreditWallet
{
    public Guid UserId { get; set; }

    public int Balance { get; set; }

    // Study AI may take credits without asking once its quota is used up; media always asks
    public bool AutoUseForStudy { get; set; }

    public required DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
