using Application.Videos;
using Shouldly;

namespace UnitTests.Videos;

public class YouTubeLinkTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?feature=share&v=dQw4w9WgXcQ&t=42")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=abc")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ")]
    [InlineData("  https://www.youtube.com/watch?v=dQw4w9WgXcQ  ")]
    public void VideoId_UsualAddresses_AreRead(string link)
    {
        YouTubeLink.VideoId(link).ShouldBe("dQw4w9WgXcQ");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a link")]
    [InlineData("https://vimeo.com/123456")]
    [InlineData("https://www.youtube.com.evil.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=short")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ%22%3E")]
    [InlineData("https://www.youtube.com/channel/UC1234567890")]
    [InlineData("ftp://youtu.be/dQw4w9WgXcQ")]
    public void VideoId_OtherLinks_AreNotVideos(string? link)
    {
        YouTubeLink.VideoId(link).ShouldBeNull();
    }

    [Fact]
    public void WatchUrl_IsThePlainWatchPage()
    {
        YouTubeLink.WatchUrl("dQw4w9WgXcQ").ShouldBe("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
    }
}
