using Application.Ai.Prompts;
using Application.Exceptions;
using Application.Videos;
using Domain.Utility.Question;
using Shouldly;

namespace UnitTests.Videos;

public class VideoTranscriptPromptTests
{
    [Fact]
    public void BuildRequest_SendsTheVideo_ClippedToTheLimit()
    {
        var request = new VideoTranscriptPrompt("dQw4w9WgXcQ").BuildRequest();

        request.Video.ShouldNotBeNull();
        request.Video.Url.ShouldBe("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        request.Video.EndSeconds.ShouldBe(VideoRules.MaxSeconds);
        request.Video.FramesPerSecond.ShouldBe(VideoRules.FramesPerSecond);
        request.UserPrompt.ShouldContain("Never translate");
    }

    [Fact]
    public void Parse_KeepsLinesInOrder_DropsUnreadableTimes()
    {
        var result = new VideoTranscriptPrompt("dQw4w9WgXcQ").Parse("""
            {"language":"en","lines":[
              {"start":"00:07","text":"Second line."},
              {"start":"00:02","text":"First line."},
              {"start":"soon","text":"No time."},
              {"start":"00:09","text":"   "},
              {"start":"45:00","text":"Past the cut."},
              {"start":"00:07","text":"Same second, kept after.\nOn one line."}
            ]}
            """);

        result.Language.ShouldBe("en");
        result.Segments.Select(x => x.Text).ShouldBe(["First line.", "Second line.", "Same second, kept after. On one line."]);
        result.Segments.Select(x => x.Start).ShouldBe([2d, 7d, 7d]);
        result.IsClipped.ShouldBeFalse();
    }

    [Fact]
    public void Parse_SpeechUpToTheCut_MeansTheVideoGoesOn()
    {
        var result = new VideoTranscriptPrompt("dQw4w9WgXcQ").Parse("""
            {"language":"uk","lines":[{"start":"00:01","text":"Привіт."},{"start":"29:40","text":"І наостанок."}]}
            """);

        result.IsClipped.ShouldBeTrue();
    }

    [Fact]
    public void Parse_UnknownLanguage_IsMarkedUndetermined()
    {
        var result = new VideoTranscriptPrompt("dQw4w9WgXcQ").Parse("""{"language":"English!","lines":[]}""");

        result.Language.ShouldBe("und");
        result.Segments.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_NotJson_IsAnOutputError()
    {
        Should.Throw<AiOutputException>(() => new VideoTranscriptPrompt("dQw4w9WgXcQ").Parse("not json"));
    }
}

public class QuestionsFromVideoPromptTests
{
    private static QuestionGenerationPrompt Prompt(bool fromVideo) =>
        new("Lecture", "[00:05] Plants make sugar from light.", 2, null, [], fromVideo);

    [Fact]
    public void FromVideo_AsksForTheMoment()
    {
        var request = Prompt(fromVideo: true).BuildRequest();

        request.UserPrompt.ShouldContain("videoAt");
        request.OutputSchema.GetRawText().ShouldContain("videoAt");
    }

    [Fact]
    public void FromText_HasNoMoment()
    {
        var request = Prompt(fromVideo: false).BuildRequest();

        request.OutputSchema.GetRawText().ShouldNotContain("videoAt");
    }

    [Fact]
    public void FromVideo_ReadsTheMomentOfEveryQuestion()
    {
        var questions = Prompt(fromVideo: true).Parse("""
            {"questions":[
              {"type":"trueFalse","title":"Plants make sugar from light","difficulty":"easy","answer":"","options":[],"isTrue":true,"explanation":"Said at the start","videoAt":"00:05"},
              {"type":"open","title":"What do plants make?","difficulty":"medium","answer":"Sugar","options":[],"isTrue":false,"explanation":"","videoAt":"later"}
            ]}
            """);

        questions[0].VideoSeconds.ShouldBe(5);
        questions[0].Type.ShouldBe(QuestionType.TrueFalse);
        // An unreadable moment leaves the question without one
        questions[1].VideoSeconds.ShouldBeNull();
    }
}
