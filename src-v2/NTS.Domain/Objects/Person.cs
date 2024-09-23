using Newtonsoft.Json;

namespace NTS.Domain.Objects;

public class Person : IComparable<Person>
{
    internal static string DELIMITER = " ";

    [JsonConstructor]
    private Person(params string[] names) 
    {
        this.Names = names;
    }
 
    public Person(string names)
    {
        this.Names = names.Split(DELIMITER);
    }

    public string[] Names { get; private set; } = Array.Empty<string>();// TODO: test record equality

    public override string ToString()
	{
		return string.Join(DELIMITER, this.Names);
	}

    public int CompareTo(Person? other)
    {
        if (other == null) return 1;

        // First compare Athlete
        int athleteComparison = string.Compare(String.Join(DELIMITER, Names), String.Join(" ", other.Names), StringComparison.Ordinal);
        return athleteComparison;
    }

    public static implicit operator string(Person member)
    {
        return member.ToString();
    }
}
