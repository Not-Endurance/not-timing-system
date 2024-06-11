using Newtonsoft.Json;

namespace NTS.Domain.Objects;

public class Person
{
    internal static string DELIMITER = " ";

    [JsonConstructor]
    private Person(string[] names)
    {
        Names = names;
    }
    public Person(string name)
    {
        this.Names = name.Split(DELIMITER);
    }

    public string[] Names { get; private set; } = Array.Empty<string>();// TODO: test record equality

    public override string ToString()
	{
		return string.Join(DELIMITER, this.Names);
	}

    public static implicit operator string(Person member)
    {
        return member.ToString();
    }
}
