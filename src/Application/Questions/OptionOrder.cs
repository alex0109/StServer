using Domain.Entities;
using Domain.Utility.Question;

namespace Application.Questions;

// Choices are stored in the order they were written, and the right one often comes first.
// Each run (an attempt, a review session) shows them in an order of its own, the same on every load.
// True/false keeps its order: "true" first is a convention, not a hint
public static class OptionOrder
{
    public static List<Option> For(Guid runId, Question question)
    {
        var options = question.Options.OrderBy(o => o.Position).ToArray();

        if (question.QuestionType == QuestionType.Options)
            StableRandom.For(runId, question.Id).Shuffle(options);

        return options.ToList();
    }
}
