using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs.Account;

namespace Application.Account;

// The export is built in memory and sent right away: nothing is stored, so there is no
// download link to protect and nothing to clean up. One user's study data is a few megabytes at most
public static class AccountExportArchive
{
    public const string DataFileName = "studytracker-data.json";
    public const string WordsFileName = "words.csv";
    public const string ReadmeFileName = "README.txt";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        // Cyrillic and Norwegian letters stay readable in the file
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string FileName(DateTime exportedAt) => $"studytracker-export-{exportedAt:yyyy-MM-dd}.zip";

    public static byte[] Build(AccountExportDto data)
    {
        using var buffer = new MemoryStream();

        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, DataFileName, JsonSerializer.SerializeToUtf8Bytes(data, Json));
            Add(zip, WordsFileName, WordsCsv(data));
            Add(zip, ReadmeFileName, Encoding.UTF8.GetBytes(Readme));
        }

        return buffer.ToArray();
    }

    // Columns Anki and spreadsheets understand; the BOM makes Excel read the file as UTF-8
    public static byte[] WordsCsv(AccountExportDto data)
    {
        var csv = new StringBuilder();
        csv.Append("term,translation,transcription,partOfSpeech,example,exampleTranslation,dictionary\r\n");

        foreach (var dictionary in data.Dictionaries)
        {
            foreach (var word in dictionary.Words)
            {
                csv.AppendJoin(',',
                    Cell(word.Term),
                    Cell(word.Translation),
                    Cell(word.Transcription),
                    Cell(word.PartOfSpeech?.ToString()),
                    Cell(word.Example),
                    Cell(word.ExampleTranslation),
                    Cell(dictionary.Title));
                csv.Append("\r\n");
            }
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    // RFC 4180 quoting. A leading = + @ makes spreadsheets run the cell as a formula, so it gets an apostrophe;
    // a leading "-" is left alone, word parts like "-ness" are common in a dictionary
    public static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        if (value[0] is '=' or '+' or '@' or '\t' or '\r') value = "'" + value;

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static void Add(ZipArchive zip, string name, byte[] content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content);
    }

    private const string Readme =
        """
        Studytracker data export

        studytracker-data.json  Everything the app stores about your account: materials, notes and drawings
                                (editor JSON as stored), questions with options, attempts and answers,
                                dictionaries and words, review history, tags, library requests and
                                published materials, reminder settings, plan and AI usage.
        words.csv               All words in one table. In Anki: File > Import, pick this file,
                                fields are separated by commas, the first line is the header.

        Times are UTC. Attached files are listed by name only.
        """;
}
