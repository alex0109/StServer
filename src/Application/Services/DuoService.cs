using System.Security.Cryptography;
using System.Text;
using Application.Billing;
using Application.DTOs.Billing;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Utility.Subscription;

namespace Application.Services;

// Duo is Plus for two people. The payer sends a link, the person who opens it joins. The member's
// plan follows the payer's subscription: when it ends or changes to Plus, both are back on Free
public class DuoService : IDuoService
{
    public static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

    private readonly IBillingRepository _repo;
    private readonly IUserContext _user;
    private readonly TimeProvider _time;

    public DuoService(IBillingRepository repo, IUserContext user, TimeProvider time)
    {
        _repo = repo;
        _user = user;
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<DuoStatusDto> GetAsync()
    {
        var now = Now;
        var pays = await PaysForDuoAsync(_user.UserId, now);
        var owned = await _repo.GetDuoAsync(_user.UserId);

        if (!pays)
        {
            var joined = await _repo.FindDuoByMemberAsync(_user.UserId);
            if (joined is not null)
            {
                return new DuoStatusDto(DuoRole.Member, await PaysForDuoAsync(joined.UserId, now), false,
                    null, null, joined.JoinedAt, joined.OwnerEmail);
            }

            // A lapsed Duo still holds its member, who comes back when the payer renews
            if (owned?.MemberUserId is null) return new DuoStatusDto(DuoRole.None, false, false, null, null, null, null);
        }

        var inviteOpen = pays && owned?.InviteTokenHash is not null && owned.InviteExpiresAt > now;

        return new DuoStatusDto(DuoRole.Owner, pays, pays && owned?.MemberUserId is null,
            inviteOpen ? owned!.InviteExpiresAt : null, owned?.MemberEmail, owned?.JoinedAt, null);
    }

    public async Task<DuoInviteDto> CreateInviteAsync()
    {
        var now = Now;
        if (!await PaysForDuoAsync(_user.UserId, now)) throw new BadRequestException("Inviting needs a Duo plan");

        var duo = await _repo.GetDuoAsync(_user.UserId);
        if (duo?.MemberUserId is not null) throw new ConflictException("The Duo has its second person already");

        if (duo is null)
        {
            duo = new DuoMembership { UserId = _user.UserId, CreatedAt = now };
            await _repo.AddDuoAsync(duo);
        }

        // A new link replaces the old one, so a link sent to the wrong person can be taken back
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        duo.InviteTokenHash = Hash(token);
        duo.InviteExpiresAt = now + InviteLifetime;
        duo.OwnerEmail = _user.Email;
        duo.UpdatedAt = now;
        await _repo.SaveChangesAsync();

        return new DuoInviteDto(token, duo.InviteExpiresAt.Value);
    }

    public async Task CancelInviteAsync()
    {
        var duo = await _repo.GetDuoAsync(_user.UserId);
        if (duo?.InviteTokenHash is null) return;

        duo.InviteTokenHash = null;
        duo.InviteExpiresAt = null;
        duo.UpdatedAt = Now;
        await _repo.SaveChangesAsync();
    }

    public async Task<DuoInvitePreviewDto> PreviewAsync(string token)
    {
        var duo = await OpenInviteAsync(token, Now);
        return new DuoInvitePreviewDto(duo.OwnerEmail, duo.UserId == _user.UserId);
    }

    public async Task JoinAsync(string token)
    {
        var now = Now;
        var duo = await OpenInviteAsync(token, now);

        if (duo.UserId == _user.UserId) throw new BadRequestException("This is your own invite");
        // Joining would leave their own subscription running and paid for nothing
        if (SubscriptionRules.EffectiveTier(await _repo.GetSubscriptionAsync(_user.UserId), now) != SubscriptionTier.Free)
            throw new ConflictException("There is a plan of your own already");
        if (await _repo.FindDuoByMemberAsync(_user.UserId) is not null)
            throw new ConflictException("You are on another Duo already");

        duo.MemberUserId = _user.UserId;
        duo.MemberEmail = _user.Email;
        duo.JoinedAt = now;
        duo.InviteTokenHash = null;
        duo.InviteExpiresAt = null;
        duo.UpdatedAt = now;
        await _repo.SaveChangesAsync();
    }

    public async Task RemoveMemberAsync()
    {
        var duo = await _repo.GetDuoAsync(_user.UserId);
        if (duo?.MemberUserId is null) return;

        ClearMember(duo);
        await _repo.SaveChangesAsync();
    }

    public async Task LeaveAsync()
    {
        var duo = await _repo.FindDuoByMemberAsync(_user.UserId);
        if (duo is null) return;

        ClearMember(duo);
        await _repo.SaveChangesAsync();
    }

    private void ClearMember(DuoMembership duo)
    {
        duo.MemberUserId = null;
        duo.MemberEmail = null;
        duo.JoinedAt = null;
        duo.UpdatedAt = Now;
    }

    // An expired link, a used one and one whose payer no longer has Duo all look the same: gone
    private async Task<DuoMembership> OpenInviteAsync(string token, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) throw new KeyNotFoundException("No such invite");

        var duo = await _repo.FindDuoByInviteAsync(Hash(token.Trim()));
        if (duo is null || duo.InviteExpiresAt <= now || !await PaysForDuoAsync(duo.UserId, now))
            throw new KeyNotFoundException("No such invite");

        return duo;
    }

    // Only the payer's own subscription: a member can't invite a third person
    private async Task<bool> PaysForDuoAsync(Guid userId, DateTime now) =>
        SubscriptionRules.EffectiveTier(await _repo.GetSubscriptionAsync(userId), now) == SubscriptionTier.Duo;

    private static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
