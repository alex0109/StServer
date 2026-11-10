using Application.Ai.Prompts;
using Application.DTOs.Ai;
using Application.Interfaces;
using Application.Profile;

namespace Application.Services.Ai;

public class WordExplanationService : IWordExplanationService
{
    private readonly IDictionaryRepository _dictionaries;
    private readonly IAiService _ai;
    private readonly IUserContext _user;
    private readonly IUserProfileService? _profiles;

    public WordExplanationService(
        IDictionaryRepository dictionaries,
        IAiService ai,
        IUserContext user,
        IUserProfileService? profiles = null)
    {
        _dictionaries = dictionaries;
        _ai = ai;
        _user = user;
        _profiles = profiles;
    }

    public async Task<WordExplanationDto?> ExplainAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        var entry = await _dictionaries.GetEntryByIdAsync(entryId, _user.UserId);
        if (entry is null) return null;

        // The language chosen for AI explanations; without one, the language of the saved translation
        var language = _profiles is null
            ? ProfileLanguages.QuestionLanguage
            : await _profiles.GetExplanationLanguageAsync(_user.UserId);

        var prompt = new WordExplanationPrompt(
            entry.Term,
            entry.Article,
            entry.Translation,
            entry.PartOfSpeech,
            entry.Example,
            ProfileLanguages.PromptName(language));

        var explanation = (await _ai.RunAsync(prompt, cancellationToken)).Value;

        return new WordExplanationDto
        {
            Term = entry.Term,
            Meaning = explanation.Meaning,
            Examples = explanation.Examples
                .Select(x => new WordUsageExampleDto { Sentence = x.Sentence, Translation = x.Translation, Situation = x.Situation })
                .ToList(),
            Collocations = explanation.Collocations.ToList(),
            Tip = explanation.Tip,
        };
    }
}
