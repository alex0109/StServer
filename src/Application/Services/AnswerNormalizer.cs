using System.Text;
using System.Text.RegularExpressions;
using StServer.Application.Interfaces;

namespace StServer.Application.Services;

public class AnswerNormalizer : IAnswerNormalizer
{
    public string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value.Trim().ToLowerInvariant();

        value = Regex.Replace(value, @"\s+", " ");

        value = value.Normalize(NormalizationForm.FormKC);

        value = Regex.Replace(value, @"[^\p{L}\p{N}\s]", "");

        return value;
    }
}