using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Not.Domain.Exceptions;
using Not.Structures;
using static Not.Localization.NStrings;

namespace Not.Domain;

public abstract class InLineEntityValidator
{
    Type? _thisType;

    protected T NotDefault<T>(string field, T value)
        where T : struct
    {
        if (value.Equals(default))
        {
            throw GetRequiredException(field);
        }
        return value;
    }

    protected T Required<T>(string field, T? value)
        where T : struct
    {
        return value ?? throw GetRequiredException(field);
    }

    [return: NotNull]
    protected T Required<T>(string field, T? instance)
        where T : class
    {
        return instance ?? throw GetRequiredException(field);
    }

    [return: NotNull]
    protected string Required(string field, [NotNull] string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw GetRequiredException(field);
        }
        return value;
    }

    protected IEnumerable<T> AreUnique<T>(string field, IEnumerable<T> collection)
        where T : IIdentifiable
    {
        if (collection.GroupBy(x => x.Id).Select(x => x.Count()).Any(x => x != 1))
        {
            throw new DomainPropertyException(field, "Collection_contains_duplicate_entries"); // TODO: why string?
        }
        return collection;
    }

    DomainPropertyException GetRequiredException(string field)
    {
        _thisType ??= GetType();
        return new DomainPropertyException(
            field,
            string.Format(Field_1_is_required_on_2_string, field, _thisType.Name)
        );
    }
}
