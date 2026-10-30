using System.Globalization;
using System.Text;

namespace Nepply.Services;

internal static class SearchText
{
    internal static string Normalize(string text)
    {
        var result = new StringBuilder(text.Length);
        string decomposed;
        try { decomposed = text.Normalize(NormalizationForm.FormD); }
        catch (ArgumentException)
        {
            // Shell metadata can contain unpaired UTF-16 surrogates. Repair only
            // the search projection; never rewrite the file's actual identity.
            var valid = new StringBuilder(text.Length);
            foreach (var rune in text.EnumerateRunes()) valid.Append(rune.ToString());
            decomposed = valid.ToString().Normalize(NormalizationForm.FormD);
        }
        foreach (char c in decomposed.ToLowerInvariant())
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                result.Append(c == 'đ' ? 'd' : char.IsWhiteSpace(c) ? ' ' : c);
        return result.ToString();
    }
    internal static string[] Words(string query) => Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
    internal static bool Matches(string name, IReadOnlyList<string> words)
    {
        if (words.Count == 0) return true;
        string normalized = Normalize(name);
        return words.All(word => normalized.Contains(word, StringComparison.Ordinal));
    }
}
