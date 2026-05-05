using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Not.Exceptions;

/// <summary>
/// GuardHelper deals with exceptions that should never be seen by the end-user
/// </summary>
public static class GuardHelper
{
    /// <summary>
    /// Mainly used in order to prevent nullable warnings and guard against default values
    /// </summary>
    /// <exception cref="GuardException"></exception>
    // [DoesNotReturn]
    public static T ThrowIfDefault<T>(
        [NotNull] T? value,
        [CallerFilePath] string? callerFilePath = null,
        [CallerMemberName] string? callerMemberName = null
    )
    {
        ThrowIfDefault(value, $"'{typeof(T).Name}' cannot be default in {callerFilePath}.{callerMemberName}");
        return value;
    }

    public static T ThrowIfDefault<T>([NotNull] T? value, string message)
    {
        if (value?.Equals(default(T)) ?? true)
        {
            throw new GuardException(message);
        }
        return value;
    }

    public static void ThrowIfNullOrWhiteSpace(object value, [CallerArgumentExpression(nameof(value))] string? argument = null)
    {
        if (string.IsNullOrWhiteSpace(value?.ToString()))
        {
            throw new GuardException($"'{argument}' cannot be null or whitespace");
        }
    }

    public static Exception Exception(string message)
    {
        return new GuardException(message);
    }
}
