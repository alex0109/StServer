using Application.Questions;
using Domain.Entities;
using Domain.Utility.Question;

namespace Application.Attempts;

// Which questions an attempt has and in what order. They are drawn once, at the start, so a reload
// or the end of a timed attempt sees the same set
public static class AttemptQuestions
{
    // A random set of the active questions of the chosen levels, already in the order they are shown.
    // No levels means all of them, no count means every question that matches
    public static List<Guid> Draw(
        IEnumerable<Question> activeQuestions,
        IReadOnlyCollection<QuestionDifficulty> difficulties,
        int? count,
        Random random)
    {
        var pool = activeQuestions
            .Where(q => difficulties.Count == 0 || difficulties.Contains(q.QuestionDifficulty))
            .Select(q => q.Id)
            .ToArray();

        random.Shuffle(pool);

        return pool.Take(count ?? pool.Length).ToList();
    }

    // The attempt's questions that are still active, in the attempt's order. An attempt from before
    // the draw has every active question, in an order of its own that stays the same on every load
    public static List<Question> Of(Attempt attempt, IReadOnlyCollection<Question> activeQuestions)
    {
        if (attempt.QuestionIds.Count == 0)
        {
            var all = activeQuestions.OrderBy(q => q.Id).ToArray();
            StableRandom.For(attempt.Id, Guid.Empty).Shuffle(all);
            return all.ToList();
        }

        var byId = activeQuestions.ToDictionary(q => q.Id);

        return attempt.QuestionIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .ToList();
    }
}
