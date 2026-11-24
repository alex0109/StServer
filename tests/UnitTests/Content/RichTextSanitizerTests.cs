using System.Text.Json;
using Application.DTOs.RichTextDocument;
using Shouldly;

namespace UnitTests.Content;

public class RichTextSanitizerTests
{
    // Read the way stored or posted editor JSON is read, attribute values arrive as JsonElement
    private static RichTextDocument Clean(string json)
    {
        using var document = JsonDocument.Parse(json);
        return RichTextJson.Parse(document).ShouldNotBeNull();
    }

    private static Dictionary<string, object?> LinkAttrs(RichTextDocument document) =>
        document.Content[0].Content![0].Marks![0].Attrs.ShouldNotBeNull();

    private static string Link(string href) =>
        """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"click","marks":[{"type":"link","attrs":{"href":"""
        + JsonSerializer.Serialize(href)
        + ""","target":"_blank"}}]}]}]}""";

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData(" java\tscript:alert(1)")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("data:image/svg+xml;base64,PHN2Zz48L3N2Zz4=")]
    [InlineData("&#106;avascript:alert(1)")]
    public void ScriptLinks_LoseTheirAddress_ButKeepTheRest(string href)
    {
        var attrs = LinkAttrs(Clean(Link(href)));

        attrs.ShouldNotContainKey("href");
        attrs.ShouldContainKey("target");
    }

    [Theory]
    [InlineData("https://example.com/page?q=1#part")]
    [InlineData("http://example.com")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("tel:+4712345678")]
    [InlineData("/materials/1")]
    [InlineData("#heading")]
    [InlineData("page.html")]
    public void WebLinks_StayAsTheyAre(string href)
    {
        var attrs = LinkAttrs(Clean(Link(href)));

        ((JsonElement)attrs["href"]!).GetString().ShouldBe(href);
    }

    [Fact]
    public void PastedRasterImages_StayButSvgAndHtmlDoNot()
    {
        var document = Clean("""
            {"type":"doc","content":[
              {"type":"image","attrs":{"src":"data:image/png;base64,iVBORw0KGgo=","alt":"a"}},
              {"type":"image","attrs":{"src":"data:image/svg+xml,<svg onload=alert(1)>","alt":"b"}},
              {"type":"image","attrs":{"src":"https://example.com/cat.webp"}}
            ]}
            """);

        document.Content[0].Attrs.ShouldNotBeNull().ShouldContainKey("src");
        document.Content[1].Attrs.ShouldNotBeNull().ShouldNotContainKey("src");
        document.Content[1].Attrs!.ShouldContainKey("alt");
        document.Content[2].Attrs.ShouldNotBeNull().ShouldContainKey("src");
    }

    [Fact]
    public void EventHandlers_AndScriptValuesUnderOtherNames_AreDropped()
    {
        var document = Clean("""
            {"type":"doc","content":[{"type":"paragraph","attrs":{
              "onclick":"alert(1)","onError":"alert(1)","srcdoc":"<script></script>",
              "customLink":"javascript:alert(1)",
              "nested":{"href":"javascript:alert(1)"},
              "textAlign":"center","level":2,"color":"#ff0000"
            }}]}
            """);

        var attrs = document.Content[0].Attrs.ShouldNotBeNull();
        attrs.Keys.OrderBy(x => x).ShouldBe(["color", "level", "textAlign"]);
    }

    [Fact]
    public void NodesAndMarksWithImpossibleTypes_AreDropped()
    {
        var document = Clean("""
            {"type":"doc","content":[
              {"type":"img src=x onerror=alert(1)","content":[{"type":"text","text":"x"}]},
              {"type":"paragraph","content":[{"type":"text","text":"ok","marks":[{"type":"<b>"},{"type":"bold"}]}]}
            ]}
            """);

        document.Content.ShouldHaveSingleItem().Type.ShouldBe("paragraph");
        document.Content[0].Content![0].Marks.ShouldNotBeNull().ShouldHaveSingleItem().Type.ShouldBe("bold");
    }

    // Text saved before the sanitizer existed, or copied by a share link, is cleaned on the way out too
    [Fact]
    public void ClientShape_IsCleanedAsWell()
    {
        using var stored = JsonDocument.Parse(Link("javascript:alert(1)"));

        var client = RichTextJson.ToClient(stored.RootElement);

        client.GetRawText().ShouldNotContain("javascript", Case.Insensitive);
    }
}
