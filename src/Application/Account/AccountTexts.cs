using Application.Notifications;

namespace Application.Account;

// The last email the app sends to a deleted account
public static class AccountTexts
{
    public record DeletedEmail(string Subject, string Title, List<string> Lines);

    public static string Language(string? requested) =>
        requested is not null && NotificationOptions.Languages.Contains(requested)
            ? requested
            : NotificationOptions.DefaultLanguage;

    public static DeletedEmail Deleted(string language) => language switch
    {
        "uk" => new(
            "Твій акаунт Studytracker видалено",
            "Акаунт видалено",
            [
                "Ми видалили твій акаунт і всі матеріали, нотатки, питання, словники та історію навчання.",
                "Твої матеріали в бібліотеці лишаються там без імені автора, бо інші вже зробили з них копії.",
                "Якщо ти цього не робив чи не робила, просто відповідай на цей лист.",
            ]),
        "nb" => new(
            "Studytracker-kontoen din er slettet",
            "Kontoen er slettet",
            [
                "Vi har slettet kontoen din og alle materialer, notater, spørsmål, ordbøker og læringshistorikk.",
                "Materialer du publiserte i biblioteket blir liggende der uten navnet ditt, fordi andre allerede har kopiert dem.",
                "Hvis det ikke var deg, kan du svare på denne e-posten.",
            ]),
        _ => new(
            "Your Studytracker account has been deleted",
            "Account deleted",
            [
                "We have deleted your account and all your materials, notes, questions, dictionaries and study history.",
                "Materials you published in the library stay there without your name, because others have already copied them.",
                "If this wasn't you, just reply to this email.",
            ]),
    };
}
