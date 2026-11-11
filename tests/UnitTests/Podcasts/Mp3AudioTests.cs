using Application.Podcasts;
using Shouldly;

namespace UnitTests.Podcasts;

public class Mp3AudioTests
{
    [Fact]
    public void Duration_SumsTheFrames()
    {
        Mp3Audio.Duration(Mp3Fixtures.Speech(250)).ShouldBe(6.0, 0.0001);
        Mp3Audio.Duration(Mp3Fixtures.Music(100)).ShouldBe(100 * 1152 / 44100.0, 0.0001);
    }

    [Fact]
    public void Duration_SkipsTheTagAndNoise()
    {
        byte[] noisy = [1, 2, 3, .. Mp3Fixtures.Speech(10)];

        Mp3Audio.Duration(Mp3Fixtures.Speech(10, withId3: true)).ShouldBe(0.24, 0.0001);
        Mp3Audio.Duration(noisy).ShouldBe(0.24, 0.0001);
        Mp3Audio.Duration([]).ShouldBe(0);
    }

    [Fact]
    public void Join_KeepsEveryFrame_AndDropsTags()
    {
        var joined = Mp3Audio.Join([Mp3Fixtures.Speech(5, withId3: true), Mp3Fixtures.Speech(7, withId3: true)]);

        joined.Length.ShouldBe(12 * 144);
        Mp3Audio.Duration(joined).ShouldBe(12 * 0.024, 0.0001);
    }
}
