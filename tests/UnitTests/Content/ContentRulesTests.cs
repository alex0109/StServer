using System.Text.Json;
using Application.Content;
using Application.DTOs.RichTextDocument;
using Application.Exceptions;
using Shouldly;

namespace UnitTests.Content;

public class ContentRulesTests
{
    [Fact]
    public void Title_OverTheLimit_IsABadRequest_NullMeansUnchanged()
    {
        Should.Throw<BadRequestException>(() => ContentRules.EnsureTitle(new string('a', 201), ContentRules.MaxNoteTitleLength, "Note"));
        Should.NotThrow(() => ContentRules.EnsureTitle(new string('a', 200), ContentRules.MaxNoteTitleLength, "Note"));
        Should.NotThrow(() => ContentRules.EnsureTitle(null, ContentRules.MaxNoteTitleLength, "Note"));
    }

    [Fact]
    public void RichText_OfNormalSize_ReturnsItsSize()
    {
        var document = new RichTextDocument
        {
            Content = [new Node { Type = "paragraph", Content = [new Node { Type = "text", Text = "Hello" }] }],
        };

        ContentRules.EnsureRichText(document, "The note text").ShouldBeGreaterThan(0);
        ContentRules.EnsureRichText(null, "The note text").ShouldBe(0);
    }

    [Fact]
    public void RichText_WithTooManyNodes_IsABadRequest()
    {
        var document = new RichTextDocument
        {
            Content = Enumerable.Range(0, ContentRules.MaxRichTextNodes + 1).Select(_ => new Node { Type = "paragraph" }).ToList(),
        };

        Should.Throw<BadRequestException>(() => ContentRules.EnsureRichText(document, "The note text"));
    }

    [Fact]
    public void RichText_NestedTooDeeply_IsABadRequest()
    {
        var node = new Node { Type = "blockquote" };
        var root = node;
        for (var i = 0; i < ContentRules.MaxRichTextDepth; i++)
        {
            var child = new Node { Type = "blockquote" };
            node.Content = [child];
            node = child;
        }

        Should.Throw<BadRequestException>(() => ContentRules.EnsureRichText(new RichTextDocument { Content = [root] }, "The note text"));
    }

    [Fact]
    public void RichText_TooLarge_IsABadRequest()
    {
        var document = new RichTextDocument
        {
            Content = [new Node { Type = "text", Text = new string('a', ContentRules.MaxRichTextChars) }],
        };

        Should.Throw<BadRequestException>(() => ContentRules.EnsureRichText(document, "The note text"));
    }

    [Fact]
    public void Drawing_TooLarge_IsABadRequest_NullIsNothing()
    {
        using var huge = JsonDocument.Parse(JsonSerializer.Serialize(new { strokes = new string('1', ContentRules.MaxDrawingChars) }));
        using var empty = JsonDocument.Parse("null");

        Should.Throw<BadRequestException>(() => ContentRules.EnsureDrawing(huge));
        ContentRules.EnsureDrawing(empty).ShouldBe(0);
        ContentRules.EnsureDrawing(null).ShouldBe(0);
    }
}
