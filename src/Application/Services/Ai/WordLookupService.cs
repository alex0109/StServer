using Application.Ai;
using Application.Ai.Prompts;
using Application.DTOs.Ai;
using Application.Exceptions;
using Application.Interfaces;
using Application.Limits;
using Application.Profile;
using Application.Services;

namespace Application.Services.Ai;

public class WordLookupService : IWordLookupService
{
    private const string FallbackLanguage = "English";

    private readonly IDictionaryRepository _dictionaries;
    private readonly IAiService _ai;
    private readonly IUserContext _user;
    private readonly IContentLimits _limits;
    private readonly IUserProfileRepository? _profiles;

    public WordLookupService(
        IDictionaryRepository dictionaries,
        IAiService ai,
        IUserContext user,
        IContentLimits? limits = null,
        IUserProfileRepository? profiles = null)
    {
        _dictionaries = dictionaries;
        _ai = ai;
        _user = user;
        _limits = limits ?? ContentLimits.Free();
        _profiles = profiles;
    }

    public async Task<WordLookupDto?> LookupAsync(
        Guid dictionaryId,
        WordLookupRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var term = AiContentSanitizer.Clean(dto.Term, WordLookupPrompt.MaxTermLength);
        if (term.Length == 0)
            throw new BadRequestException("Select a word or a phrase");

        var userId = _user.UserId;
        var dictionary = await _dictionaries.GetByIdAsync(dictionaryId, userId);
        if (dictionary is null) return null;

        var existingId = await _dictionaries.FindEntryIdByTermAsync(dictionaryId, term);

        // A full dictionary can't take the word, so the request would be paid for nothing
        var max = (await _limits.ForUserAsync(userId)).EntriesPerDictionary;
        if (existingId is null && max is int limit && await _dictionaries.CountEntriesAsync(dictionaryId) >= limit)
            throw new LimitExceededException($"Word limit reached: {limit} per dictionary");

        var language = await LanguageAsync(dto.Language, userId);
        var sentence = AiContentSanitizer.Clean(dto.Sentence, WordLookupPrompt.MaxSentenceLength);

        var lookup = (await _ai.RunAsync(new WordLookupPrompt(term, sentence, language), cancellationToken)).Value;

        return new WordLookupDto
        {
            Term = term,
            Translation = lookup.Translation,
            Transcription = lookup.Transcription,
            PartOfSpeech = lookup.PartOfSpeech,
            GrammarInfo = lookup.GrammarInfo,
            Example = sentence.Length == 0 ? null : sentence,
            ExampleTranslation = lookup.ExampleTranslation,
            Language = language,
            ExistingEntryId = existingId,
        };
    }

    // The language goes into the prompt: a checked plain name, from the request or the profile
    private async Task<string> LanguageAsync(string? requested, Guid userId)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var name = requested.Trim();
            return AiLanguageName.IsValid(name)
                ? name
                : throw new BadRequestException("Language must be a language name, e.g. \"Ukrainian\"");
        }

        var profile = _profiles is null ? null : await _profiles.GetAsync(userId);
        if (AiLanguageName.IsValid(profile?.NativeLanguage)) return profile!.NativeLanguage!;

        return profile is not null && ProfileLanguages.PromptName(profile.InterfaceLanguage) is { } interfaceName
            ? interfaceName
            : FallbackLanguage;
    }
}
