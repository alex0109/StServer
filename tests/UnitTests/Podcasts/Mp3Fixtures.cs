namespace UnitTests.Podcasts;

// Silent MP3 frames built by hand, the same layout speech services return
internal static class Mp3Fixtures
{
    // MPEG 2 Layer III, 48 kbit/s, 24 kHz: 144 bytes and 24 ms a frame
    public static byte[] Speech(int frames, bool withId3 = false)
    {
        var frame = new byte[144];
        frame[0] = 0xFF;
        frame[1] = 0xF3;
        frame[2] = 0x64;
        frame[3] = 0xC4;

        var data = Enumerable.Range(0, frames).SelectMany(_ => frame).ToArray();
        return withId3 ? [.. Id3(20), .. data] : data;
    }

    // MPEG 1 Layer III, 128 kbit/s, 44.1 kHz: 417 bytes and 1152 samples a frame
    public static byte[] Music(int frames)
    {
        var frame = new byte[417];
        frame[0] = 0xFF;
        frame[1] = 0xFB;
        frame[2] = 0x90;
        frame[3] = 0x44;
        return Enumerable.Range(0, frames).SelectMany(_ => frame).ToArray();
    }

    private static byte[] Id3(int size) =>
        [(byte)'I', (byte)'D', (byte)'3', 4, 0, 0, 0, 0, 0, (byte)size, .. new byte[size]];
}
