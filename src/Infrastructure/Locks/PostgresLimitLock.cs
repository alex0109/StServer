using Application.Constants;
using Application.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Locks;

// Transaction scoped Postgres advisory lock: a second request with the same key waits
// until the first one commits, and only then counts. Released automatically on commit or rollback
public class PostgresLimitLock : ILimitLock
{
    private readonly AppDbContext _context;

    public PostgresLimitLock(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ILimitLockScope> AcquireAsync(LimitScope scope, Guid ownerId)
    {
        var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var key = ToLockKey(scope, ownerId);
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");

            return new Scope(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    // Stable 64 bit key, a collision only makes two unrelated requests wait for each other
    internal static long ToLockKey(LimitScope scope, Guid ownerId)
    {
        var bytes = ownerId.ToByteArray();
        var ownerKey = BitConverter.ToInt64(bytes, 0) ^ BitConverter.ToInt64(bytes, 8);

        return ownerKey ^ ((long)scope << 56);
    }

    private sealed class Scope : ILimitLockScope
    {
        private readonly IDbContextTransaction _transaction;

        public Scope(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync() => _transaction.CommitAsync();

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}
