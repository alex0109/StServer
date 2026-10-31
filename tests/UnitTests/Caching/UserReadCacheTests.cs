using Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;

namespace UnitTests.Caching;

public class UserReadCacheTests
{
    private readonly UserReadCache _cache = new(new MemoryCache(new MemoryCacheOptions()));
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _otherUser = Guid.NewGuid();

    [Fact]
    public async Task SecondRead_ComesFromCache()
    {
        var calls = 0;
        Task<int> Load() => Task.FromResult(++calls);

        var first = await _cache.GetOrCreateAsync(_user, "stats", Load);
        var second = await _cache.GetOrCreateAsync(_user, "stats", Load);

        first.ShouldBe(1);
        second.ShouldBe(1);
        calls.ShouldBe(1);
    }

    [Fact]
    public async Task Invalidate_MakesNextReadLoadAgain()
    {
        var calls = 0;
        Task<int> Load() => Task.FromResult(++calls);

        await _cache.GetOrCreateAsync(_user, "stats", Load);
        _cache.Invalidate(_user);
        var afterChange = await _cache.GetOrCreateAsync(_user, "stats", Load);

        afterChange.ShouldBe(2);
    }

    [Fact]
    public async Task Invalidate_KeepsOtherUsersEntries()
    {
        var calls = 0;
        Task<int> Load() => Task.FromResult(++calls);

        await _cache.GetOrCreateAsync(_otherUser, "stats", Load);
        _cache.Invalidate(_user);
        var other = await _cache.GetOrCreateAsync(_otherUser, "stats", Load);

        other.ShouldBe(1);
    }

    [Fact]
    public async Task Keys_AreSeparatePerUser()
    {
        var mine = await _cache.GetOrCreateAsync(_user, "stats", () => Task.FromResult("mine"));
        var theirs = await _cache.GetOrCreateAsync(_otherUser, "stats", () => Task.FromResult("theirs"));

        mine.ShouldBe("mine");
        theirs.ShouldBe("theirs");
    }

    [Fact]
    public async Task ExpiredEntry_IsLoadedAgain()
    {
        var calls = 0;
        Task<int> Load() => Task.FromResult(++calls);

        await _cache.GetOrCreateAsync(_user, "stats", Load, TimeSpan.FromMilliseconds(1));
        await Task.Delay(20, TestContext.Current.CancellationToken);
        var again = await _cache.GetOrCreateAsync(_user, "stats", Load, TimeSpan.FromMilliseconds(1));

        again.ShouldBe(2);
    }
}
