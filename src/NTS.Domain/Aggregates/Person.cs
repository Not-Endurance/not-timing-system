using Newtonsoft.Json;

namespace NTS.Domain.Aggregates;

public class Person
{
    public static Person? Create(string? names)
    {
        if (string.IsNullOrWhiteSpace(names))
        {
            return null;
        }
        return new Person(names.Split(DELIMITER, StringSplitOptions.RemoveEmptyEntries));
    }

    public static implicit operator string[](Person member)
    {
        return member.Names;
    }

    public static implicit operator Person(string[] names)
    {
        return new Person(names);
    }

    public static implicit operator string(Person person)
    {
        return person.ToString();
    }

    internal static string DELIMITER = " ";

    [JsonConstructor]
    public Person(string[] names)
    {
        Names = names;
    }

    public string[] Names { get; private set; } = []; // TODO: consider encapsulating this;

    public override string ToString()
    {
        return string.Join(DELIMITER, Names);
    }

    public string? GetFirstName()
    {
        return Names.First();
    }

    public string? GetLastName()
    {
        return Names.Last();
    }
}
