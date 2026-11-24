namespace Application.Essays;

// Language exams an essay can be written and checked for. Without one the general check is used
public enum EssayExam
{
    // Deutsch-Test für Zuwanderer: one test for A2-B1, its writing part is a letter
    Dtz,
    // Norskprøve A1-B2: short messages and e-mails, texts on familiar topics, argumentative texts
    Norskprove,
    // IELTS General Training writing: Task 1 a letter, Task 2 an essay
    Ielts,
    // Deutsch-Test für den Beruf B2 (telc): the German B2 exam after the integration course, work e-mails
    Dtb,
}

public enum EssayKind
{
    Essay,
    Letter,
}

// The exam, the level aimed at and the kind of text, stored on the note as "Ielts/B2/Letter".
// Only the formats the exams really have exist
public sealed record EssayFormat(EssayExam Exam, string Level, EssayKind Kind)
{
    public const int MaxCodeLength = 32;

    private static readonly Dictionary<EssayExam, (string[] Levels, EssayKind[] Kinds)> Allowed = new()
    {
        [EssayExam.Dtz] = (["A2", "B1"], [EssayKind.Letter]),
        [EssayExam.Dtb] = (["B2"], [EssayKind.Letter]),
        [EssayExam.Norskprove] = (["A1", "A2", "B1", "B2"], [EssayKind.Essay, EssayKind.Letter]),
        [EssayExam.Ielts] = (["B1", "B2"], [EssayKind.Essay, EssayKind.Letter]),
    };

    public string Code => $"{Exam}/{Level}/{Kind}";

    public override string ToString() => Code;

    public static bool TryParse(string? code, out EssayFormat? format)
    {
        format = null;
        var parts = code?.Trim().Split('/');
        if (parts is not { Length: 3 }) return false;

        if (!Enum.TryParse<EssayExam>(parts[0], ignoreCase: false, out var exam) || !Enum.IsDefined(exam)) return false;
        if (!Enum.TryParse<EssayKind>(parts[2], ignoreCase: false, out var kind) || !Enum.IsDefined(kind)) return false;

        var (levels, kinds) = Allowed[exam];
        if (!levels.Contains(parts[1]) || !kinds.Contains(kind)) return false;

        format = new EssayFormat(exam, parts[1], kind);
        return true;
    }

    // A stored code that no longer parses is a general essay
    public static EssayFormat? FromCode(string? code) => TryParse(code, out var format) ? format : null;
}
