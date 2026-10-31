using Application.Billing;

namespace Application.Account;

public record AccountExportFile(string FileName, byte[] Content);

// BillingCustomer: a paid plan's customer at the provider, to be closed there as well
public record AccountDeletionSummary(int RowsDeleted, int RowsAnonymized, BillingCustomerRef? BillingCustomer = null);

public static class AccountIds
{
    // Enough to find the event in logs and the audit, too little to point at a person
    public static string Short(Guid userId) => userId.ToString("N")[..8];
}
