using Application.DTOs.Option;
using Application.Services;
using Domain.Entities;
using Domain.Utility.Question;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace UnitTests.Questions;

// How EF saves the options of a question that is already in the database. No database is
// needed: the state EF gives each option decides whether it is inserted, updated or deleted
public class OptionTrackingTests
{
    private static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=none").Options);

    private static Question SavedQuestion(int options) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Which are planets?",
        QuestionType = QuestionType.Options,
        CreatedAt = DateTime.UtcNow,
        Options = Enumerable.Range(0, options)
            .Select(i => new Option { Id = Guid.NewGuid(), Name = $"Option {i}", Position = i, IsCorrect = i == 0 })
            .ToList(),
    };

    [Fact]
    public void NewOption_OfASavedQuestion_IsInserted()
    {
        using var db = Db();
        var question = SavedQuestion(4);
        db.Attach(question);

        var incoming = question.Options
            .Select(x => new OptionUpdateDto { Id = x.Id, Name = x.Name, IsCorrect = x.IsCorrect })
            .Append(new OptionUpdateDto { Name = "Option 4", IsCorrect = false })
            .ToList();
        new OptionService().SyncOptions(question, incoming);
        db.ChangeTracker.DetectChanges();

        var added = question.Options.Single(x => x.Name == "Option 4");
        // An update would change no row and fail the whole save
        db.Entry(added).State.ShouldBe(EntityState.Added);
        question.Options.Where(x => x != added).ShouldAllBe(x => db.Entry(x).State == EntityState.Unchanged);
    }

    [Fact]
    public void RemovedOption_OfASavedQuestion_IsDeleted()
    {
        using var db = Db();
        var question = SavedQuestion(3);
        db.Attach(question);
        var removed = question.Options.Last();

        new OptionService().SyncOptions(question, question.Options
            .Take(2)
            .Select(x => new OptionUpdateDto { Id = x.Id, Name = x.Name, IsCorrect = x.IsCorrect })
            .ToList());
        db.ChangeTracker.DetectChanges();

        db.Entry(removed).State.ShouldBe(EntityState.Deleted);
    }
}
