using Not.Exceptions;
using Not.Extensions;

namespace Not.Structures;

public class NotListModel
{
    public static IEnumerable<NotListModel<T>> FromEnum<T>()
        where T : struct, Enum
    {
        var values = Enum.GetValues(typeof(T));
        foreach (var value in values)
        {
            var enumValue = (T)value;
            yield return new NotListModel<T>(enumValue, enumValue.GetDescription());
        }
    }

    /// <summary>
    /// Get enum values for <typeparamref name="T"/> without enum constraint.
    /// </summary>
    /// <typeparam name="T">Type of enum</typeparam>
    /// <param name="type">Type of enum. Must match <typeparamref name="T"/></param>
    /// <exception cref="GuardException">If <paramref name="type"/> doesnt match <typeparamref name="T"/></exception>
    /// <exception cref="GuardException">If <typeparamref name="T"/> is not an Enum type</exception>
    /// <returns></returns>
    public static IEnumerable<NotListModel<T>> FromEnum<T>(Type type)
    {
        var underlyingT = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (!underlyingType.IsEnum)
        {
            throw GuardHelper.Exception($"Type '{underlyingType}' is not an enum.");
        }
        if (underlyingType != underlyingT)
        {
            throw GuardHelper.Exception($"Type '{type}' cannot be assigned to '{typeof(T)}'.");
        }

        var values = Enum.GetValues(underlyingType);
        foreach (var value in values)
        {
            var enumValue = (Enum)value;
            var description = Localize(enumValue);
            var tValue = (T)Enum.ToObject(underlyingType, value);
            yield return new NotListModel<T>(tValue, description);
        }
    }

    public static IEnumerable<NotListModel<T>> FromEntity<T>(IEnumerable<T> values)
    {
        foreach (var value in values)
        {
            yield return new NotListModel<T>(value);
        }
    }
}

public class NotListModel<T>
{
    public NotListModel(T value, string? label = null)
    {
        Value = value;
        Label = label ?? value!.ToString()!;
    }

    public T? Value { get; } = default!;
    public string Label { get; }
}
