using Application.Exceptions;
using Application.Interfaces;
using Application.Limits;

namespace Application.Services;

public class ContentLimits : IContentLimits
{
    private readonly ISubscriptionTierProvider _tiers;
    private readonly IMaterialRepository _materials;
    private readonly ContentLimitsOptions _options;

    public ContentLimits(ISubscriptionTierProvider tiers, IMaterialRepository materials, ContentLimitsOptions options)
    {
        _tiers = tiers;
        _materials = materials;
        _options = options;
    }

    // Default plan rules without a database lookup: everyone is on Free. For services built by hand (tests)
    public static IContentLimits Free(IMaterialRepository? materials = null) =>
        new ContentLimits(new FreeTier(), materials!, new ContentLimitsOptions());

    private sealed class FreeTier : ISubscriptionTierProvider
    {
        public Task<Domain.Utility.Subscription.SubscriptionTier> GetTierAsync(Guid userId) =>
            Task.FromResult(Domain.Utility.Subscription.SubscriptionTier.Free);

        public Task<Billing.AiPlanPeriod> GetAiPlanAsync(Guid userId)
        {
            var now = DateTime.UtcNow;
            var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            return Task.FromResult(new Billing.AiPlanPeriod(Domain.Utility.Subscription.SubscriptionTier.Free, month, month.AddMonths(1)));
        }
    }

    public bool CountLibraryImports => _options.CountLibraryImports;

    public async Task<TierLimits> ForUserAsync(Guid userId) =>
        _options.For(await _tiers.GetTierAsync(userId));

    public async Task EnsureMaterialPlaceAsync(Guid userId, bool libraryCopy)
    {
        var limits = await ForUserAsync(userId);

        if (libraryCopy && !_options.CountLibraryImports)
        {
            if (limits.LibraryImports is int imports && await _materials.CountLibraryCopiesAsync(userId) >= imports)
                throw new LimitExceededException($"Library import limit reached: {imports}");
            return;
        }

        if (limits.Materials is not int max) return;

        var used = _options.CountLibraryImports
            ? await _materials.CountByUserIdAsync(userId)
            : await _materials.CountOwnByUserIdAsync(userId);

        if (used >= max)
            throw new LimitExceededException($"Material limit reached: {max}");
    }

    public async Task EnsureStorageAsync(Guid userId, long addingChars)
    {
        // Services built by hand without a repository (tests) have nothing to measure
        if (addingChars <= 0 || _options.StorageMb <= 0 || _materials is null) return;

        var limit = _options.StorageMb * 1024L * 1024L;
        var used = await _materials.GetStoredContentBytesAsync(userId);

        if (used + addingChars > limit)
            throw new LimitExceededException($"Storage limit reached: {_options.StorageMb} MB of texts and drawings");
    }
}
