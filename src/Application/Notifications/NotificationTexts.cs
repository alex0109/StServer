using System.Globalization;
using Application.DTOs.Notification;

namespace Application.Notifications;

// Email texts in the languages of the site. In-app reminders are worded on the client
public static class NotificationTexts
{
    public record EmailText(string Subject, string Title, List<string> Lines, string Button);

    public static EmailText DailyReview(string language, DailyReviewData data) => language switch
    {
        "uk" => new(
            $"📚 Сьогодні на тебе чекають {data.DueWords} {Uk(data.DueWords, "слово", "слова", "слів")}",
            "Час повторити слова",
            [
                $"Сьогодні час повторити {data.DueWords} {Uk(data.DueWords, "слово", "слова", "слів")}"
                + (data.NewWords > 0 ? $" і ще є {data.NewWords} {Uk(data.NewWords, "нове", "нові", "нових")}." : "."),
                "Кілька хвилин зараз — і вони залишаться в пам'яті на тижні.",
            ],
            "Повторити зараз"),
        "nb" => new(
            $"📚 {data.DueWords} ord venter på deg i dag",
            "Tid for å repetere",
            [
                $"I dag er det på tide å repetere {data.DueWords} ord"
                + (data.NewWords > 0 ? $", og {data.NewWords} nye venter også." : "."),
                "Noen minutter nå, og de sitter i minnet i ukevis.",
            ],
            "Repeter nå"),
        _ => new(
            $"📚 {data.DueWords} {En(data.DueWords, "word is", "words are")} waiting for you today",
            "Time to review",
            [
                $"Today it's time to review {data.DueWords} {En(data.DueWords, "word", "words")}"
                + (data.NewWords > 0 ? $", and {data.NewWords} new {En(data.NewWords, "one is", "ones are")} waiting too." : "."),
                "A few minutes now keeps them in your memory for weeks.",
            ],
            "Review now"),
    };

    public static EmailText StreakAtRisk(string language, StreakAtRiskData data) => language switch
    {
        "uk" => new(
            $"🔥 Серія з {data.StreakDays} {Uk(data.StreakDays, "дня", "днів", "днів")} обірветься опівночі",
            "Серія під загрозою",
            [
                "Сьогодні ще не було жодного заняття.",
                "Однієї короткої сесії достатньо, щоб зберегти серію.",
            ],
            "Зберегти серію"),
        "nb" => new(
            $"🔥 Rekken din på {data.StreakDays} {(data.StreakDays == 1 ? "dag" : "dager")} slutter ved midnatt",
            "Rekken er i fare",
            [
                "Du har ikke studert i dag ennå.",
                "Én kort økt er nok til å holde rekken i live.",
            ],
            "Behold rekken"),
        _ => new(
            $"🔥 Your {data.StreakDays}-day streak ends at midnight",
            "Your streak is at risk",
            [
                "You haven't studied today yet.",
                "One short session is enough to keep the streak going.",
            ],
            "Keep the streak"),
    };

    public static EmailText WeeklyDigest(string language, WeeklyDigestData data)
    {
        var topics = string.Join(", ", data.WeakestTopics.Select(x => $"{x.Name} ({Format(x.Accuracy)}%)"));

        return language switch
        {
            "uk" => new(
                "📊 Твій тиждень у Studytracker",
                "Підсумки тижня",
                Lines(
                    $"Днів із навчанням: {data.StudyDays} з 7",
                    $"Серія: {data.StreakDays} {Uk(data.StreakDays, "день", "дні", "днів")} поспіль",
                    data.Accuracy is null ? null : $"Відповідей: {data.Answers}, точність {Format(data.Accuracy.Value)}%",
                    data.Reviews > 0 ? $"Повторено карток: {data.Reviews}" : null,
                    topics.Length > 0 ? $"Варто підтягнути: {topics}" : null),
                "Переглянути статистику"),
            "nb" => new(
                "📊 Uka di i Studytracker",
                "Ukens oppsummering",
                Lines(
                    $"Dager med studier: {data.StudyDays} av 7",
                    $"Rekke: {data.StreakDays} {(data.StreakDays == 1 ? "dag" : "dager")} på rad",
                    data.Accuracy is null ? null : $"Svar: {data.Answers}, treffsikkerhet {Format(data.Accuracy.Value)} %",
                    data.Reviews > 0 ? $"Repeterte kort: {data.Reviews}" : null,
                    topics.Length > 0 ? $"Verdt å øve mer på: {topics}" : null),
                "Se statistikk"),
            _ => new(
                "📊 Your week in Studytracker",
                "Your week in review",
                Lines(
                    $"Days with study: {data.StudyDays} of 7",
                    $"Streak: {data.StreakDays} {En(data.StreakDays, "day", "days")} in a row",
                    data.Accuracy is null ? null : $"Answers: {data.Answers}, accuracy {Format(data.Accuracy.Value)}%",
                    data.Reviews > 0 ? $"Flashcards reviewed: {data.Reviews}" : null,
                    topics.Length > 0 ? $"Worth more practice: {topics}" : null),
                "See statistics"),
        };
    }

    public static string Footer(string language) => language switch
    {
        "uk" => "Цей лист надіслано, бо в Studytracker увімкнено нагадування на пошту. Вимкнути їх можна на сторінці статистики.",
        "nb" => "Du får denne e-posten fordi påminnelser på e-post er slått på i Studytracker. Du kan slå dem av på statistikksiden.",
        _ => "You get this email because email reminders are on in Studytracker. You can turn them off on the statistics page.",
    };

    public static string ManageLink(string language) => language switch
    {
        "uk" => "Керувати нагадуваннями",
        "nb" => "Administrer påminnelser",
        _ => "Manage reminders",
    };

    private static List<string> Lines(params string?[] lines) => lines.OfType<string>().ToList();

    private static string Format(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string En(int count, string one, string many) => count == 1 ? one : many;

    // Ukrainian has three plural forms: 1 слово, 2 слова, 5 слів
    private static string Uk(int count, string one, string few, string many)
    {
        var lastTwo = count % 100;
        var last = count % 10;

        if (lastTwo is >= 11 and <= 14) return many;
        return last switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many,
        };
    }
}
