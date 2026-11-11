namespace Application.Podcasts;

// Enough of the MP3 format to know how long a recording is and to join recordings of the same
// voice settings: MPEG frames stand on their own, so joined frames play as one file
public static class Mp3Audio
{
    private static readonly int[] Mpeg1Layer3Bitrates = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320];
    private static readonly int[] Mpeg2Layer3Bitrates = [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160];
    private static readonly int[] Mpeg1SampleRates = [44100, 48000, 32000];

    // Seconds of sound in the frames of a recording
    public static double Duration(ReadOnlySpan<byte> data)
    {
        var position = AfterId3(data);
        double seconds = 0;

        while (position + 4 <= data.Length)
        {
            if (TryReadFrame(data[position..], out var length, out var frameSeconds))
            {
                seconds += frameSeconds;
                position += length;
            }
            else
            {
                // Not a frame start: look for the next one
                position++;
            }
        }

        return seconds;
    }

    // One recording from several; tags at the start of the parts are dropped
    public static byte[] Join(IEnumerable<byte[]> parts)
    {
        using var output = new MemoryStream();
        foreach (var part in parts)
        {
            var start = AfterId3(part);
            output.Write(part, start, part.Length - start);
        }

        return output.ToArray();
    }

    private static int AfterId3(ReadOnlySpan<byte> data)
    {
        if (data.Length < 10 || data[0] != 'I' || data[1] != 'D' || data[2] != '3') return 0;

        // Size is "synchsafe": seven bits per byte; a footer adds ten more bytes
        var size = (data[6] & 0x7F) << 21 | (data[7] & 0x7F) << 14 | (data[8] & 0x7F) << 7 | (data[9] & 0x7F);
        var hasFooter = (data[5] & 0x10) != 0;

        return Math.Min(data.Length, 10 + size + (hasFooter ? 10 : 0));
    }

    private static bool TryReadFrame(ReadOnlySpan<byte> data, out int length, out double seconds)
    {
        length = 0;
        seconds = 0;
        if (data.Length < 4 || data[0] != 0xFF || (data[1] & 0xE0) != 0xE0) return false;

        var version = (data[1] >> 3) & 0x03; // 0: MPEG 2.5, 2: MPEG 2, 3: MPEG 1
        var layer = (data[1] >> 1) & 0x03; // 1: Layer III
        var bitrateIndex = (data[2] >> 4) & 0x0F;
        var sampleRateIndex = (data[2] >> 2) & 0x03;
        var padding = (data[2] >> 1) & 0x01;

        if (version == 1 || layer != 1 || bitrateIndex is 0 or 15 || sampleRateIndex == 3) return false;

        var isMpeg1 = version == 3;
        var bitrate = (isMpeg1 ? Mpeg1Layer3Bitrates : Mpeg2Layer3Bitrates)[bitrateIndex] * 1000;
        var sampleRate = Mpeg1SampleRates[sampleRateIndex] / (isMpeg1 ? 1 : version == 2 ? 2 : 4);
        var samples = isMpeg1 ? 1152 : 576;

        length = (isMpeg1 ? 144 : 72) * bitrate / sampleRate + padding;
        if (length < 4) return false;

        seconds = (double)samples / sampleRate;
        return true;
    }
}
