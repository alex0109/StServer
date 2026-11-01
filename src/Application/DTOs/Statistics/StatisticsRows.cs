using Domain.Utility.Question;
using Domain.Utility.Result;
using Domain.Utility.Review;

namespace Application.DTOs.Statistics;

// Only the fields statistics are counted from: no answer texts, questions or material content
public record AnswerStatRow(
    QuestionDifficulty Difficulty,
    bool IsCorrect,
    TimeSpan TimeSpent,
    ConfidenceLevel? ConfidenceLevel);

// Answers grouped by the tags of their material, counted by the database
public record TagAnswerStat(Guid TagId, string TagName, int Answers, int Correct);

// One flashcard review: which word, which button, when (UTC)
public record FlashcardAnswerRow(Guid EntryId, ReviewRating Rating, DateTime ReviewedAt);

public record WordTerm(Guid EntryId, string Term, string Translation);
