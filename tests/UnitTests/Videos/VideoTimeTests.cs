using Application.Videos;
using Shouldly;

namespace UnitTests.Videos;

public class VideoTimeTests
{
    [Theory]
    [InlineData("00:00", 0)]
    [InlineData("3:12", 192)]
    [InlineData("03:12", 192)]
    [InlineData("12:05.6", 725)]
    [InlineData("1:02:05", 3725)]
    [InlineData(" 07:09 ", 429)]
    public void Parse_UsualForms(string value, int seconds)
    {
        VideoTime.Parse(value).ShouldBe(seconds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("192")]
    [InlineData("3:75")]
    [InlineData("1:75:00")]
    [InlineData("a:12")]
    [InlineData("-1:12")]
    [InlineData("1:2:3:4")]
    public void Parse_Nonsense_IsNull(string? value)
    {
        VideoTime.Parse(value).ShouldBeNull();
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(192.7, "03:12")]
    [InlineData(3725, "1:02:05")]
    public void Format_ReadsLikeAPlayer(double seconds, string text)
    {
        VideoTime.Format(seconds).ShouldBe(text);
    }
}
