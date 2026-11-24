using Application.Exceptions;
using Application.Profile;
using Domain.Entities;

namespace Application.Ai;

// The language a word is translated into: the one the request names, else the user's native
// language, else the interface's. Always a checked plain name, because it goes into a prompt
public static class AiTranslationLanguage
{
    public const string Fallback = "English";

    public static Task<string> ResolveAsync(string? requested, UserProfile? profile)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var name = requested.Trim();
            return Task.FromResult(AiLanguageName.IsValid(name)
                ? name
                : throw new BadRequestException("Language must be a language name, e.g. \"Ukrainian\""));
        }

        if (AiLanguageName.IsValid(profile?.NativeLanguage)) return Task.FromResult(profile!.NativeLanguage!);

        return Task.FromResult(profile is not null && ProfileLanguages.PromptName(profile.InterfaceLanguage) is { } interfaceName
            ? interfaceName
            : Fallback);
    }
}
