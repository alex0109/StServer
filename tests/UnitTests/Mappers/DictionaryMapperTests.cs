using Application.DTOs.Dictionary;
using Application.Mappers;
using Domain.Entities;
using Domain.Utility.Dictionary;
using Shouldly;

namespace UnitTests.Mappers;

public class DictionaryMapperTests
{
    private static DictionaryEntry Noun(string? article) => new()
    {
        Id = Guid.NewGuid(),
        Term = "Hund",
        Translation = "собака",
        PartOfSpeech = PartOfSpeech.Noun,
        Article = article,
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public void ToEntryEntity_Noun_KeepsTheTrimmedArticle()
    {
        var entry = DictionaryMapper.ToEntryEntity(
            new DictionaryEntryCreateDto { Term = "Hund", Translation = "собака", PartOfSpeech = PartOfSpeech.Noun, Article = " der " },
            Guid.NewGuid(), 1);

        entry.Article.ShouldBe("der");
        DictionaryMapper.ToEntryDto(entry).Article.ShouldBe("der");
    }

    [Theory]
    [InlineData(PartOfSpeech.Verb)]
    [InlineData(null)]
    public void ToEntryEntity_NotANoun_HasNoArticle(PartOfSpeech? partOfSpeech)
    {
        var entry = DictionaryMapper.ToEntryEntity(
            new DictionaryEntryCreateDto { Term = "laufen", Translation = "бігти", PartOfSpeech = partOfSpeech, Article = "der" },
            Guid.NewGuid(), 1);

        entry.Article.ShouldBeNull();
    }

    [Fact]
    public void ToEntryEntity_BlankArticle_IsNone()
    {
        var entry = DictionaryMapper.ToEntryEntity(
            new DictionaryEntryCreateDto { Term = "Hund", Translation = "собака", PartOfSpeech = PartOfSpeech.Noun, Article = "  " },
            Guid.NewGuid(), 1);

        entry.Article.ShouldBeNull();
    }

    [Fact]
    public void ApplyEntryUpdate_NoArticleSent_KeepsIt()
    {
        var entry = Noun("der");

        DictionaryMapper.ApplyEntryUpdate(entry, new DictionaryEntryUpdateDto { Translation = "пес" });

        entry.Article.ShouldBe("der");
    }

    [Fact]
    public void ApplyEntryUpdate_EmptyArticle_ClearsIt()
    {
        var entry = Noun("der");

        DictionaryMapper.ApplyEntryUpdate(entry, new DictionaryEntryUpdateDto { Article = "" });

        entry.Article.ShouldBeNull();
    }

    [Fact]
    public void ApplyEntryUpdate_NewArticle_ReplacesIt()
    {
        var entry = Noun("die");

        DictionaryMapper.ApplyEntryUpdate(entry, new DictionaryEntryUpdateDto { Article = "der" });

        entry.Article.ShouldBe("der");
    }

    [Fact]
    public void ApplyEntryUpdate_NoLongerANoun_LosesTheArticle()
    {
        var entry = Noun("der");

        DictionaryMapper.ApplyEntryUpdate(entry, new DictionaryEntryUpdateDto { PartOfSpeech = PartOfSpeech.Adjective, Article = "der" });

        entry.Article.ShouldBeNull();
    }
}
