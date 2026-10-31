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
}
