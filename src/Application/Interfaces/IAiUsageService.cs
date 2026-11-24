using Application.Ai;
using Application.DTOs.Ai;
using Domain.Utility.Ai;

namespace Application.Interfaces;

// What paid for a reserved AI request
public record AiReservation(Guid Id, AiUsageSource Source, int Units, int Credits);

// The usage gate: every AI request reserves its units first, then commits what it really used or
// releases them. Quota of the plan's period first, then Free's one-time trial, then credits
public interface IAiUsageService
{
    // Throws AiUsageDeniedException when neither quota nor (allowed) credits cover the units
    // allowCredits false: only quota or trial may pay
    Task<AiReservation> ReserveAsync(Guid userId, AiFeature feature, int units = 1, bool allowCredits = true);

    // The request answered: real tokens and cost; a video may take fewer units than reserved
    Task CommitAsync(Guid usageId, AiCompletion completion, int? units = null, int? videoSeconds = null);

    // No provider answered: quota or credits go back
    Task ReleaseAsync(Guid usageId, AiCompletion? completion = null);

    // A provider answered (and billed it) with nothing the user can use. Released like a failure for the
    // first AiOptions.UnusableGracePerDay of the day per feature, committed like a usable answer after that
    Task SettleUnusableAsync(Guid usageId, AiCompletion completion);

    Task<AiUsageResponseDto> GetUsageAsync(Guid userId);

    // Requests left pending by a lost process give their quota or credits back
    Task<int> ReleaseStaleAsync();
}
