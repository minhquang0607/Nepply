using System.Collections.Immutable;

namespace Nepply;

/// <summary>Language-neutral UI message. Arguments are data, never format strings.</summary>
public sealed class UserMessage
{
    private const string ExceptionKey = "Nepply.UserMessageKey";
    public string? Key { get; }
    public string? OriginalText { get; }
    public ImmutableArray<object?> Arguments { get; }
    public string Separator { get; }
    private UserMessage(string? key, string? text, object?[] arguments, string separator = "")
        => (Key, OriginalText, Arguments, Separator) = (key, text, arguments.ToImmutableArray(), separator);
    public static UserMessage FromKey(string key, params object?[] arguments) => new(key, null, arguments);
    // Untranslatable detail such as an OS exception message; rendered verbatim, never looked up.
    public static UserMessage FromText(string text) => new(null, text, []);
    public static UserMessage Join(IEnumerable<UserMessage> messages, string separator = "\n")
        => new(null, null, messages.Cast<object?>().ToArray(), separator);
    // A key attached by Nepply code wins; any other exception keeps its own detail.
    public static UserMessage From(Exception error)
        => error.Data[ExceptionKey] is string key ? FromKey(key) : FromText(error.Message);
    internal static TException Attach<TException>(TException error, string key) where TException : Exception
    {
        error.Data[ExceptionKey] = key;
        return error;
    }
}

internal static class UserMessageExceptions
{
    /// <summary>Marks an exception whose user-facing text is the resource <paramref name="key"/>; its Message stays for logs.</summary>
    internal static TException WithUserMessage<TException>(this TException error, string key) where TException : Exception
        => UserMessage.Attach(error, key);
}
