namespace Application.DTOs.Admin;

// Money is in the currency's smallest unit (cents, øre), the way Stripe counts it.
// Periods are calendar ones in UTC, from their first day to now
public record AdminMoneyPeriodsDto(long Month, long Quarter, long Year);

// One currency. Gross: what customers paid, VAT included. Net: what reached the account after
// Stripe's fees, taxes and refunds; null in a currency no money is kept in. MonthlyRecurring: what
// the plans that renew bring a month. ProjectedYear: the year's gross if every one of them renews
public record AdminRevenueDto(
    string Currency, AdminMoneyPeriodsDto Gross, AdminMoneyPeriodsDto? Net, long MonthlyRecurring, long ProjectedYear);

// Plans in force now. Plus and Duo are paid and past their trial; Granted were given by hand;
// Canceling still run but end with the period. New and Canceled are the last 30 days
public record AdminBillingCountsDto(
    int Plus, int Duo, int Trialing, int PastDue, int Canceling, int Granted, int DuoMembers,
    int NewLast30Days, int CanceledLast30Days);

// One calendar month: subscriptions started, cancellations asked for (or plans ended without asking),
// money in the main currency
public record AdminBillingMonthDto(string Month, int New, int Canceled, long Gross, long Net);

public record AdminBillingStatsDto(
    // Stripe is set up and answered; otherwise only what the database knows is shown
    bool RevenueAvailable,
    string? RevenueError,
    // The currency of the chart: the one most money came in
    string? MainCurrency,
    AdminBillingCountsDto Counts,
    List<AdminRevenueDto> Revenue,
    List<AdminBillingMonthDto> Months,
    DateTime GeneratedAt);

// A provider's webhook as the statistics need it, from the stored event
public record SubscriptionEventRow(string? SubscriptionId, Guid? UserId, string Type, DateTime OccurredAt, bool CancelAtPeriodEnd);
