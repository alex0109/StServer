using Application.DTOs.Admin;
using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Admin;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Admin;

public class AdminAudit : IAdminAudit
{
    private readonly AppDbContext _db;
    private readonly IUserContext _user;
    private readonly TimeProvider _time;

    public AdminAudit(AppDbContext db, IUserContext user, TimeProvider time)
    {
        _db = db;
        _user = user;
        _time = time;
    }

    public async Task RecordAsync(AdminAction action, Guid? targetId = null, string? details = null)
    {
        _db.AdminAuditEntries.Add(new AdminAuditEntry
        {
            Id = Guid.NewGuid(),
            AdminUserId = _user.UserId,
            Action = action,
            TargetId = targetId,
            Details = details is null ? null : details[..Math.Min(details.Length, 500)],
            Ip = _user.ClientIp,
            UserAgent = _user.UserAgent,
            CreatedAt = _time.GetUtcNow().UtcDateTime,
        });

        await _db.SaveChangesAsync();
    }

    public Task<List<AdminAuditEntryDto>> ListAsync(int take) =>
        _db.AdminAuditEntries
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new AdminAuditEntryDto(x.Id, x.AdminUserId, x.Action, x.TargetId, x.Details, x.Ip, x.UserAgent, x.CreatedAt))
            .ToListAsync();
}
