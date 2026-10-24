using System.Text.Json;
using Application.Ai.Text;
using Application.DTOs.RichTextDocument;
using Shouldly;

namespace UnitTests.Ai;

public class RichTextConverterTests
{
    [Fact]
    public void ToPlainText_ReadsEditorDocument()
    {
        // Same shape and casing the API stores
        var document = JsonSerializer.SerializeToDocument(new RichTextDocument
        {
            Content =
            [
                new Node { Type = "heading", Content = [new Node { Type = "text", Text = "Title" }] },
                new Node { Type = "paragraph", Content = [new Node { Type = "text", Text = "Plants make energy." }] },
                new Node
                {
                    Type = "bulletList",
                    Content =
                    [
                        new Node
                        {
                            Type = "listItem",
                            Content = [new Node { Type = "paragraph", Content = [new Node { Type = "text", Text = "Light" }] }],
                        },
                    ],
                },
            ],
        });

        var text = RichTextConverter.ToPlainText(document);

        text.ShouldContain("Title\n");
        text.ShouldContain("Plants make energy.\n");
        text.ShouldContain("- Light");
    }

    [Fact]
    public void ToPlainText_CamelCaseDocument_IsRead()
    {
        var document = JsonDocument.Parse("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Hello"}]}]}""");

        RichTextConverter.ToPlainText(document).ShouldBe("Hello");
    }

    [Fact]
    public void FromSections_BuildsHeadingsParagraphsAndLists()
    {
        var document = RichTextConverter.FromSections([new SummarySection("Intro", ["Text"], ["Fact 1", "Fact 2"])]);

        document.Content.Select(x => x.Type).ShouldBe(["heading", "paragraph", "bulletList"]);
        document.Content[2].Content!.Count.ShouldBe(2);
        RichTextConverter.ToPlainText(document).ShouldContain("- Fact 2");
    }
}
