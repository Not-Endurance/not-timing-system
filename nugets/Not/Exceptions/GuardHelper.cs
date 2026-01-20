using System.Collections;
using System.Diagnostics.CodeAnalysis;
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
    public static T ThrowIfDefault<T>([NotNull] T? value)
    {
        ThrowIfDefault(value, " cannot be default.");
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

    public static void ThrowIfEmpty<T>(IEnumerable<T> enumerable)
    {
        if (!enumerable.Any())
        {
            throw new GuardException($"Collection cannot be empty");
        }
    }

    public static Exception Exception(string message)
    {
        return new GuardException(message);
    }
}
