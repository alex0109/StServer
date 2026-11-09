using Application.DTOs.Dictionary;
using Application.Interfaces;
using Application.WordQuiz;

namespace Application.Services;

public class WordQuizService : IWordQuizService
{
    // Enough to find three different wrong choices even when some words share a translation
    private const int CandidatesPerSource = 12;

    private readonly IDictionaryRepository _dictionaries;
    private readonly IUserContext _user;
    private readonly Random _random;

    public WordQuizService(IDictionaryRepository dictionaries, IUserContext user, Random? random = null)
    {
        _dictionaries = dictionaries;
        _user = user;
        _random = random ?? Random.Shared;
    }

    public async Task<WordQuizDto?> GetQuizAsync(Guid entryId)
    {
        var entry = await _dictionaries.GetEntryByIdAsync(entryId, _user.UserId);
        if (entry is null) return null;

        var candidates = await _dictionaries.GetQuizCandidatesAsync(entry.Id, entry.DictionaryId, _user.UserId, CandidatesPerSource);

        return WordQuizBuilder.Build(entry, candidates, _random);
    }
}
