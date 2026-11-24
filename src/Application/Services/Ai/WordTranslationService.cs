using Application.Ai;
using Application.Ai.Prompts;
using Application.DTOs.Ai;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Utility.Subscription;

namespace Application.Services.Ai;

// The translation suggested while a word is typed into a dictionary. A small extra of Plus: it shares the
// "words from text" quota, never spends credits, and stays quiet instead of failing — the field is
// simply left for the user to fill
public class WordTranslationService : IWordTranslationService
{
    private readonly IAiService _ai;
    private readonly IUserContext _user;
    private readonly ISubscriptionTierProvider _tiers;
    private readonly IUserProfileRepository _profiles;

    public WordTranslationService(IAiService ai, IUserContext user, ISubscriptionTierProvider tiers, IUserProfileRepository profiles)
    {
        _ai = ai;
        _user = user;
        _tiers = tiers;
        _profiles = profiles;
    }

    public async Task<WordTranslationDto> TranslateAsync(WordTranslateDto dto, CancellationToken cancellationToken = default)
    {
        var term = AiContentSanitizer.Clean(dto.Term, WordTranslationPrompt.MaxTermLength);
        if (term.Length == 0) throw new BadRequestException("Type a word or a phrase");

        var userId = _user.UserId;
        var profile = await _profiles.GetAsync(userId);
        if (profile?.AutoTranslate == false) return WordTranslationDto.Skip(WordTranslationSkip.Off);
        if (await _tiers.GetTierAsync(userId) == SubscriptionTier.Free) return WordTranslationDto.Skip(WordTranslationSkip.Plan);

        var language = await AiTranslationLanguage.ResolveAsync(dto.Language, profile);

        try
        {
            var result = await _ai.RunAsync(
                new WordTranslationPrompt(term, language),
                // Asked again with every typed word: a superseded request stops with the client
                new AiUnits<string>(1, AllowCredits: false, StopsWithClient: true),
                x => x.Length > 0,
                "AI gave no translation",
                cancellationToken);
            return new WordTranslationDto(result.Value, null);
        }
        catch (AiUsageDeniedException)
        {
            return WordTranslationDto.Skip(WordTranslationSkip.Quota);
        }
        catch (Exception exception) when (exception is AiUnavailableException or AiOutputException)
        {
            return WordTranslationDto.Skip(WordTranslationSkip.Unavailable);
        }
    }
}
