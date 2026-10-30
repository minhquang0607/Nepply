using System.Globalization;
using System.Text.Json;
using System.Windows;

namespace Nepply.Services;

public static class LocalizationService
{
    public sealed record LanguageOption(string Id, string Name) { public override string ToString() => Name; }
    public static IReadOnlyList<LanguageOption> Languages { get; } = Array.AsReadOnly(new[] {
        new LanguageOption("vi","Tiếng Việt"),new("en","English"),new("ja","日本語"),new("de","Deutsch"),
        new("zh","简体中文"),new("ru","Русский"),new("fr","Français"),new("th","ไทย") });
    private static readonly Dictionary<string, string[]> Strings = Load();
    private static int _index;
    private static bool _initialized;
    public static string CurrentLanguage => Languages[_index].Id;
    internal static event Action? Changed;
    public static bool IsSupported(string? language) => Languages.Any(l => l.Id == language);
    /// <summary>First-run language: the Windows display language when Nepply has it, otherwise English.</summary>
    public static string SystemDefault => DefaultFor(CultureInfo.CurrentUICulture);
    internal static string DefaultFor(CultureInfo culture)
    {
        // Only Simplified Chinese is translated; Traditional Chinese readers get English.
        for (var current = culture; !string.IsNullOrEmpty(current.Name); current = current.Parent)
            if (current.Name is "zh-Hant" or "zh-TW" or "zh-HK" or "zh-MO") return "en";
        string language = culture.TwoLetterISOLanguageName;
        return IsSupported(language) ? language : "en";
    }
    private static Dictionary<string, string[]> Load()
    {
        using var stream = typeof(LocalizationService).Assembly.GetManifestResourceStream("Nepply.Resources.Strings.json")
            ?? throw new InvalidOperationException("Missing language resources.");
        var values = JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream)!;
        if (values.Values.Any(v => v.Length != Languages.Count || v.Any(string.IsNullOrWhiteSpace)))
            throw new InvalidOperationException("Incomplete language resources.");
        foreach (var row in values.Values)
        {
            int arguments = System.Text.CompositeFormat.Parse(row[0]).MinimumArgumentCount;
            if (row.Any(value => System.Text.CompositeFormat.Parse(value).MinimumArgumentCount != arguments))
                throw new InvalidOperationException("Inconsistent language format arguments.");
        }
        return values;
    }
    public static string Get(string key) => Strings.TryGetValue(key, out var row) ? row[_index] : key;
    internal static bool TryGet(string key, out string text)
    {
        bool found = Strings.TryGetValue(key, out var row);
        text = found ? row![_index] : "";
        return found;
    }
    public static string Render(UserMessage? message) => message == null ? "" : message.Key == null
        ? message.OriginalText ?? string.Join(message.Separator, message.Arguments.Cast<UserMessage>().Select(Render))
        : string.Format(Get(message.Key), message.Arguments.Select(value => value is UserMessage nested ? Render(nested) : value).ToArray());
    public static void Apply(string language)
    {
        int index = Languages.ToList().FindIndex(l => l.Id == language); if (index < 0) index = 0;
        if (_initialized && index == _index) return;
        _index = index; _initialized = true;
        if (Application.Current is { } app)
            foreach (var (key, values) in Strings) app.Resources["Loc." + key] = values[index];
        Changed?.Invoke();
    }
}
