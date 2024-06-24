using Not.Domain;

namespace NTS.Domain.Objects;

public record Club(string Name) : DomainObject
{
	public override string ToString()
	{
		return Name;
	}
}
