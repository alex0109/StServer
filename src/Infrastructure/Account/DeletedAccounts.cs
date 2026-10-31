using Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Account;

// In memory, per server: long enough for every access token issued before the deletion to expire
public sealed class DeletedAccounts : IDeletedAccounts
{
    private static readonly TimeSpan Keep = TimeSpan.FromHours(2);

    private readonly IMemoryCache _cache;

    public DeletedAccounts(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Add(Guid userId) => _cache.Set(Key(userId), true, Keep);

    public bool Contains(Guid userId) => _cache.TryGetValue(Key(userId), out _);

    private static string Key(Guid userId) => $"deleted-account:{userId:N}";
}
