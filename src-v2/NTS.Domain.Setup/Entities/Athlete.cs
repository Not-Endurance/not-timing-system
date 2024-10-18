using NTS.Domain.Setup.Import;
using Newtonsoft.Json;

namespace NTS.Domain.Setup.Entities;

public class Athlete : DomainEntity, ISummarizable, IImportable
{
    public static Athlete Create(string name, string? feiId, Country country, string club, AthleteCategory category)
        => new(new Person(name), feiId, country, new Club(club), category);

    public static Athlete Update(int id, string name, string? feiId, Country country, string club, AthleteCategory category)
        => new(id, new Person(name), feiId, country, new Club(club), category);

    [JsonConstructor]
    private Athlete(int id, Person person, string? feiId, Country country, Club club, AthleteCategory category)
        : this(person, feiId, country, club, category) 
    {
        Id = id;
    }

    //TODO: consider Club as persisted across Events (MAUI's raw resources?)b
    private Athlete(Person person, string? feiId, Country country, Club club, AthleteCategory category) 
    {
        FeiId = feiId;
		Person = person ?? throw new DomainException(nameof(Person), "Name is required");
		Country = country ?? throw new DomainException(nameof(Country), "Country is required");
		Club = club ?? throw new DomainException(nameof(Club), "Club is required");
        Category = category == default ? throw new DomainException(nameof(Category), "Category is required") : category;
	}

	public string? FeiId { get; private set; }
	public Person Person { get; private set; }
	public Country Country { get; private set; }
	public Club Club { get; private set; }
    public AthleteCategory Category { get; private set; }    

	public override string ToString()
	{
        return Person.ToString();
	}
}
