using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Application.Billing;
using Application.DTOs.Billing;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Utility.Admin;

namespace Application.Services;

// Promo codes made from the admin panel and kept by the billing provider. A batch is one discount with
// as many codes as asked for; every batch and every switch-off is audited and emailed to the owner
public partial class PromoCodeAdminService : IPromoCodeAdminService
{
    public const int MaxCodes = 200;
    public const int MaxUsesPerCode = 10_000;
    public const int MaxMonths = 24;
    // Stripe's limit for a coupon's name
    private const int MaxNameLength = 40;
    private const int MaxPrefixLength = 12;
    private const int RandomLength = 6;
    // Read off paper: no 0/O, 1/I/L
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private static readonly TimeSpan LongestValidity = TimeSpan.FromDays(5 * 365);

    private readonly IBillingPromotions _promotions;
    private readonly IAdminAudit _audit;
    private readonly IAdminAlerts _alerts;
    private readonly IUserContext _user;
    private readonly TimeProvider _time;

    public PromoCodeAdminService(
        IBillingPromotions promotions,
        IAdminAudit audit,
        IAdminAlerts alerts,
        IUserContext user,
        TimeProvider time)
    {
        _promotions = promotions;
        _audit = audit;
        _alerts = alerts;
        _user = user;
        _time = time;
    }

    public async Task<AdminPromoCodesDto> ListAsync(CancellationToken cancellationToken = default) =>
        _promotions.IsConfigured
            ? new AdminPromoCodesDto(true, await _promotions.ListAsync(cancellationToken))
            : new AdminPromoCodesDto(false, []);

    public async Task<PromoBatchCreatedDto> CreateAsync(PromoBatchCreateDto dto, CancellationToken cancellationToken = default)
    {
        if (!_promotions.IsConfigured) throw new BillingUnavailableException("Payments are not set up yet");

        var now = _time.GetUtcNow().UtcDateTime;
        var batch = Validate(dto, now);
        var code = NormalizedCode(dto);

        // One code with a chosen name, or many with an optional prefix and a random tail
        var exact = dto.Count == 1 && code is not null;
        var codes = exact ? [code!] : RandomCodes(code, dto.Count, []);
        var limits = new PromoCodeLimits(dto.UsesPerCode, dto.NewCustomersOnly);

        var id = await _promotions.CreateDiscountAsync(batch, cancellationToken);
        var created = (await _promotions.AddCodesAsync(id, codes, limits, cancellationToken)).ToList();

        // A random code may clash with one in use; one more try with fresh ones
        if (!exact && created.Count < codes.Count)
        {
            var retry = RandomCodes(code, codes.Count - created.Count, codes.ToHashSet());
            created.AddRange(await _promotions.AddCodesAsync(id, retry, limits, cancellationToken));
        }

        if (created.Count == 0)
        {
            throw new BadRequestException(exact
                ? $"Stripe didn't accept the code {code}: it may be in use already"
                : "Stripe didn't make any codes, try again");
        }

        var summary = $"«{batch.Name}»: {created.Count} × {dto.UsesPerCode} використ., {Describe(batch)}";
        await _audit.RecordAsync(AdminAction.CreatePromoCodes, details: summary);
        await _alerts.SubscriptionChangedAsync($"Створено промокоди {summary}");

        return new PromoBatchCreatedDto(id, created, dto.Count);
    }

    public async Task<int> DeactivateAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!IdPattern().IsMatch(id)) throw new BadRequestException("No such batch of promo codes");
        if (!_promotions.IsConfigured) throw new BillingUnavailableException("Payments are not set up yet");

        var switchedOff = await _promotions.DeactivateAsync(id, cancellationToken);

        await _audit.RecordAsync(AdminAction.DeactivatePromoCodes, details: $"{id}: вимкнено кодів {switchedOff}");
        await _alerts.SubscriptionChangedAsync($"Вимкнено промокоди {id}: {switchedOff} шт.");

        return switchedOff;
    }

    private PromoBatch Validate(PromoBatchCreateDto dto, DateTime now)
    {
        var name = dto.Name?.ReplaceLineEndings(" ").Trim() ?? "";
        if (name.Length is < 3 or > MaxNameLength) throw new BadRequestException($"Name the batch, 3 to {MaxNameLength} characters");
        if (!Enum.IsDefined(dto.Kind)) throw new BadRequestException("Unknown kind of promo code");
        if (dto.Count is < 1 or > MaxCodes) throw new BadRequestException($"Make 1 to {MaxCodes} codes at a time");
        if (dto.UsesPerCode is < 1 or > MaxUsesPerCode) throw new BadRequestException($"A code can be used 1 to {MaxUsesPerCode} times");
        if (dto.Plan is { } plan && !Enum.IsDefined(plan)) throw new BadRequestException("Unknown plan");

        DateTime? expires = null;
        if (dto.ExpiresAt is { } until)
        {
            expires = DateTime.SpecifyKind(until, DateTimeKind.Utc);
            if (expires <= now || expires > now + LongestValidity)
                throw new BadRequestException("The end date must be in the future and within 5 years");
        }

        int? months = null;
        var duration = dto.Duration;
        var percent = 0;

        if (dto.Kind == PromoKind.FreeMonths)
        {
            if (dto.Plan is null) throw new BadRequestException("Choose the plan the free months are for");
            months = ValidMonths(dto.Months);
            duration = PromoDuration.Months;
        }
        else
        {
            if (dto.PercentOff is < 1 or > 100) throw new BadRequestException("The discount must be 1 to 100 percent");
            if (!Enum.IsDefined(duration)) throw new BadRequestException("Unknown discount duration");
            if (duration == PromoDuration.Months) months = ValidMonths(dto.Months);
            percent = dto.PercentOff;
        }

        return new PromoBatch(
            name, dto.Kind, dto.Plan, percent, duration, months,
            dto.Count * dto.UsesPerCode, dto.NewCustomersOnly, expires, _user.UserId);
    }

    private static int ValidMonths(int? months) =>
        months is >= 1 and <= MaxMonths ? months.Value : throw new BadRequestException($"Choose 1 to {MaxMonths} months");

    // Stripe takes letters, digits and dashes; codes are typed by people, so upper case only
    private static string? NormalizedCode(PromoBatchCreateDto dto)
    {
        var code = dto.Code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code)) return null;

        if (!CodePattern().IsMatch(code)) throw new BadRequestException("A code is 2 to 20 letters, digits and dashes");
        if (dto.Count > 1 && code.Length > MaxPrefixLength)
            throw new BadRequestException($"A prefix for many codes is at most {MaxPrefixLength} characters");

        return code;
    }

    public static List<string> RandomCodes(string? prefix, int count, HashSet<string> taken)
    {
        var codes = new List<string>(count);
        while (codes.Count < count)
        {
            var tail = new string(Enumerable.Range(0, RandomLength).Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray());
            var code = prefix is null ? tail : $"{prefix}-{tail}";
            if (taken.Add(code)) codes.Add(code);
        }

        return codes;
    }

    // For the audit log and the owner's email
    private static string Describe(PromoBatch batch) => batch.Kind == PromoKind.FreeMonths
        ? $"{batch.Months} міс. {batch.Plan} безкоштовно"
        : $"−{batch.PercentOff}% {batch.Duration switch
        {
            PromoDuration.Months => $"на {batch.Months} міс.",
            PromoDuration.Forever => "назавжди",
            _ => "на перший платіж",
        }} ({batch.Plan?.ToString() ?? "Plus і Duo"})";

    [GeneratedRegex("^[A-Z0-9][A-Z0-9-]{1,19}$")]
    private static partial Regex CodePattern();

    // Stripe's coupon ids: letters, digits, dashes and underscores
    [GeneratedRegex("^[A-Za-z0-9_-]{1,100}$")]
    private static partial Regex IdPattern();
}
