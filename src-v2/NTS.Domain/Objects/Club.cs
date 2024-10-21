using Not.Domain;

namespace NTS.Domain.Objects;

public record Club(string Name) : DomainObject
{
    public static Club? Create(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }
        return new Club(name);
    }

	public override string ToString()
	{
		return Name;
	}
}
