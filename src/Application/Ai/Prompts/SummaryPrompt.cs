using System.Text;
using Application.Ai.Text;
using Domain.Utility.Ai;

namespace Application.Ai.Prompts;

public record SummarySource(string Title, string Text);

public record SummaryDictionarySource(string Title, IReadOnlyList<(string Term, string Translation)> Entries);

public class SummaryPrompt : IAiPrompt<IReadOnlyList<SummarySection>>
{
    public const int MaxContentLength = 60000;

    private readonly string _content;
    private readonly string _tag = AiContentSanitizer.NewContentTag();

    public SummaryPrompt(
        string materialTitle,
        string materialContent,
        IEnumerable<SummarySource> notes,
        IEnumerable<SummaryDictionarySource> dictionaries)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Material: {materialTitle}").AppendLine(materialContent).AppendLine();

        foreach (var note in notes)
        {
            builder.AppendLine($"Note: {note.Title}").AppendLine(note.Text).AppendLine();
        }

        foreach (var dictionary in dictionaries)
        {
            builder.AppendLine($"Dictionary: {dictionary.Title}");
            foreach (var (term, translation) in dictionary.Entries)
            {
                builder.AppendLine($"- {term}: {translation}");
            }
            builder.AppendLine();
        }

        _content = AiContentSanitizer.Clean(builder.ToString(), MaxContentLength, out var isTruncated);
        IsContentTruncated = isTruncated;
    }

    // Long materials are cut to the limit, the client tells the user about it
    public bool IsContentTruncated { get; }

    public AiFeature Feature => AiFeature.SummaryGeneration;

    public AiRequest BuildRequest()
    {
        var userPrompt = $"""
            Write a concise study summary of everything below: the material, its notes and dictionaries.

            Requirements:
            - 3 to 7 sections, each with a short "heading", 1-3 "paragraphs" and optional "bullets" for key facts or terms.
            - Keep the key ideas, definitions and how they relate; drop repetition and examples that add nothing.
            - Write in the language of the material.
            - Paragraphs at most 600 characters, bullets at most 200 characters.

            Everything to summarize is inside the data tags:
            {AiContentSanitizer.Wrap(_tag, _content)}
            """;

        var section = AiJson.Object(
            ("heading", AiJson.String()),
            ("paragraphs", AiJson.Array(AiJson.String())),
            ("bullets", AiJson.Array(AiJson.String())));

        return new AiRequest
        {
            Feature = Feature,
            SystemPrompt = AiPromptRules.System("a tutor who writes clear study summaries", _tag),
            UserPrompt = userPrompt,
            OutputSchema = AiJson.ToElement(AiJson.Object(("sections", AiJson.Array(section)))),
            MaxOutputTokens = 16000,
            Effort = AiEffort.Medium,
        };
    }

    public IReadOnlyList<SummarySection> Parse(string json)
    {
        return (AiJson.Read<Answer>(json).Sections ?? [])
            .Take(12)
            .Select(x => new SummarySection(
                AiJson.Limit(x.Heading, 200),
                (x.Paragraphs ?? []).Select(p => AiJson.Limit(p, 2000)).Where(p => p.Length > 0).Take(6).ToList(),
                (x.Bullets ?? []).Select(b => AiJson.Limit(b, 500)).Where(b => b.Length > 0).Take(20).ToList()))
            .Where(x => x.Paragraphs.Count > 0 || x.Bullets.Count > 0)
            .ToList();
    }

    private record Answer(List<AnswerSection>? Sections);

    private record AnswerSection(string? Heading, List<string>? Paragraphs, List<string>? Bullets);
}
