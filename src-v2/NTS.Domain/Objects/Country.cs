using Not.Domain.Summary;

namespace NTS.Domain.Objects;

public record Country(string IsoCode, string Name) : DomainObject, ISummarizable
{
	public string Summarize()
	{
		return $"{Name}, {IsoCode}";
	}

	public override string ToString()
	{
		return Name;
	}
}
