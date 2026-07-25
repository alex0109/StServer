using Application.Services;
using Shouldly;

namespace UnitTests.Services;

public class AnswerNormalizerTests
{
    private readonly AnswerNormalizer _sut = new();

    [Fact]
    public void Normalize_Null_ReturnsEmptyString()
    {
        var result = _sut.Normalize(null!);

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Normalize_EmptyString_ReturnsEmptyString()
    {
        var result = _sut.Normalize("");

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Normalize_Whitespace_ReturnsEmptyString()
    {
        var result = _sut.Normalize("     ");

        result.ShouldBe(string.Empty);
    }
    
    [Fact]
    public void Normalize_TrimsSpaces()
    {
        var result = _sut.Normalize("  hello  ");

        result.ShouldBe("hello");
    }
    
    [Fact]
    public void Normalize_ConvertsToLowerCase()
    {
        var result = _sut.Normalize("HELLO World");

        result.ShouldBe("hello world");
    }

    [Fact]
    public void Normalize_ReplacesMultipleSpaces()
    {
        var result = _sut.Normalize(
            "hello     world");
        
        result.ShouldBe(
            "hello world");
    }
    
    [Fact]
    public void Normalize_RemovesPunctuation()
    {
        var result = _sut.Normalize(
            "Hello, world!");

        result.ShouldBe(
            "hello world");
    }

    [Fact]
    public void Normalize_RemovesSpecialCharacters()
    {
        var result = _sut.Normalize(
            "hello@#$%^&world");
        
        result.ShouldBe(
            "helloworld");
    }
    
    [Fact]
    public void Normalize_KeepsNumbers()
    {
        var result = _sut.Normalize(
            "C# version 12");


        result.ShouldBe(
            "c version 12");
    }

    [Fact]
    public void Normalize_KeepsLettersFromDifferentLanguages()
    {
        var result = _sut.Normalize(
            "Привіт Hello");
        
        result.ShouldBe(
            "привіт hello");
    }
    
    [Fact]
    public void Normalize_RemovesEmoji()
    {
        var result = _sut.Normalize(
            "hello 😀 world");
        
        result.ShouldBe(
            "hello  world");
    }
    
    [Fact]
    public void Normalize_UnicodeEquivalentCharacters_ShouldNormalize()
    {
        var input = "ＡＢＣ";

        var result = _sut.Normalize(input);
        
        result.ShouldBe(
            "abc");
    }

    [Fact]
    public void Normalize_ComplexSentence_ShouldApplyAllRules()
    {
        var result = _sut.Normalize(
            "  HELLO!!!   WORLD???  ");
        
        result.ShouldBe(
            "hello world");
    }

    [Fact]
    public void Normalize_OnlySpecialCharacters_ReturnsEmpty()
    {
        var result = _sut.Normalize(
            "!@#$%^&*()");

        result.ShouldBe(
            string.Empty);
    }
    
    [Fact]
    public void Normalize_NewLinesAndTabs_ShouldNormalizeSpaces()
    {
        var result = _sut.Normalize(
            "hello\n\tworld");
        
        result.ShouldBe(
            "hello world");
    }
}