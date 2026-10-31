using Application.Notifications;

namespace Application.Profile;

// Languages AI explanations can be written in. Only these names ever reach a prompt
public static class ProfileLanguages
{
    // Explain in the language the question is written in, the behaviour before the setting
    public const string QuestionLanguage = "question";

    public static readonly IReadOnlyDictionary<string, string> Explanation = new Dictionary<string, string>
    {
        ["en-GB"] = "English",
        ["uk"] = "Ukrainian",
        ["nb"] = "Norwegian Bokmål",
        ["de"] = "German",
        ["fr"] = "French",
        ["es"] = "Spanish",
        ["it"] = "Italian",
        ["pl"] = "Polish",
        ["pt"] = "Portuguese",
        ["sv"] = "Swedish",
        ["da"] = "Danish",
    };

    public static bool IsInterface(string? code) => code is not null && NotificationOptions.Languages.Contains(code);

    public static bool IsExplanation(string? code) =>
        code is not null && (code == QuestionLanguage || Explanation.ContainsKey(code));

    // The code explanations are written in: the setting, or the interface language when there is none
    public static string ResolveExplanation(string? setting, string interfaceLanguage) =>
        IsExplanation(setting) ? setting! : interfaceLanguage;

    // Prompt name for a resolved code, null means the language of the question
    public static string? PromptName(string code) =>
        Explanation.TryGetValue(code, out var name) ? name : null;
}
