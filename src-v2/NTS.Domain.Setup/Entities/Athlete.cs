using NTS.Domain.Setup.Import;
using Newtonsoft.Json;

namespace NTS.Domain.Setup.Entities;

public class Athlete : DomainEntity, ISummarizable, IImportable
{
    public static Athlete Create(string? name, string? feiId, Country? country, string? club, string? category)
        => new(Person.Create(name), feiId, country, Club.Create(club), category);

    public static Athlete Update(int id, string? name, string? feiId, Country? country, string? club, string? category)
        => new(id, Person.Create(name), feiId, country, Club.Create(club), category);

    [JsonConstructor]
    private Athlete(int id, Person? person, string? feiId, Country? country, Club? club, string? category) : base(id)
    {
        FeiId = feiId;
        Person = Required(nameof(Person), person);
        Country = Required(nameof(Country), country);
        Club = Required(nameof(Club), club);
        Category = Required(nameof(Category), category);
    }

    //TODO: consider Club as persisted across Events (MAUI's raw resources?)b
    private Athlete(Person? person, string? feiId, Country? country, Club? club, string? category)
        : this(GenerateId(), person, feiId, country, club, category)
    {
	}

	public string? FeiId { get; private set; }
	public Person Person { get; private set; }
	public Country Country { get; private set; }
	public Club Club { get; private set; }
    public string Category { get; private set; }    

	public override string ToString()
	{
        return Person.ToString();
	}
}
