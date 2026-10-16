using System.Collections.Immutable;

namespace Nepply;

/// <summary>Language-neutral UI message. Arguments are data, never format strings.</summary>
public sealed class UserMessage
{
    public string? Key { get; }
    public string? OriginalText { get; }
    public ImmutableArray<object?> Arguments { get; }
    public string Separator { get; }
    private UserMessage(string? key, string? text, object?[] arguments, string separator = "")
        => (Key, OriginalText, Arguments, Separator) = (key, text, arguments.ToImmutableArray(), separator);
    public static UserMessage FromKey(string key, params object?[] arguments) => new(key, null, arguments);
    // Bridge for existing fixed service notices; parameterized messages use keys.
    public static UserMessage FromText(string text) => new(null, text, []);
    public static UserMessage Join(IEnumerable<UserMessage> messages, string separator = "\n")
        => new(null, null, messages.Cast<object?>().ToArray(), separator);
}
