using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Not.Reflection;

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
        [CallerMemberName] string? callerMemberName = null)
    {
        ThrowIfDefault(value, $"'{0}' cannot be default in {callerFilePath}.{callerMemberName}");
        return value;
    }

    public static T ThrowIfDefault<T>([NotNull] T? value, string message)
    {
        if (value?.Equals(default(T)) ?? true)
        {
            throw new GuardException($"{ReflectionHelper.GetName<T>()} " + message);
        }
        return value;
    }

    public static Exception Exception(string message)
    {
        return new GuardException(message);
    }
}
