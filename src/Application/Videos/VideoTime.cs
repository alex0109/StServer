using System.Globalization;

namespace Application.Videos;

// Moments in a video as people and models write them: "3:12", "03:12" or "1:02:05"
public static class VideoTime
{
    public static string Format(double seconds)
    {
        var total = (int)Math.Max(0, Math.Floor(seconds));
        var hours = total / 3600;
        return hours > 0
            ? $"{hours}:{total % 3600 / 60:00}:{total % 60:00}"
            : $"{total / 60:00}:{total % 60:00}";
    }

    public static int? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var parts = value.Trim().Split(':');
        if (parts.Length is < 2 or > 3) return null;

        var numbers = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            // Fractions of a second are dropped, subtitles don't need them
            var part = parts[i].Split('.')[0];
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i])) return null;
        }

        var (hours, minutes, seconds) = numbers.Length == 3
            ? (numbers[0], numbers[1], numbers[2])
            : (0, numbers[0], numbers[1]);

        if (seconds > 59 || (numbers.Length == 3 && minutes > 59)) return null;
        return hours * 3600 + minutes * 60 + seconds;
    }
}
