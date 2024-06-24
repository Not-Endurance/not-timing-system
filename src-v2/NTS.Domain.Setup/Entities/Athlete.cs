using NTS.Domain.Setup.Import;
using Newtonsoft.Json;

namespace NTS.Domain.Setup.Entities;

public class Athlete : DomainEntity, ISummarizable, IImportable
{
    public static Athlete Create(string name, string? feiId, Country country, string club, string category) => new(new Person(name), feiId, country, new Club(club), category);

    public static Athlete Update(int id, string name, string? feiId, Country country, string club, string category) => new(id, new Person(name), feiId, country, new Club(club), category);

    [JsonConstructor]
    private Athlete(int id, Person person, string? feiId, Country country, Club club, string category) : this(person, feiId, country, club, category) 
    {
        Id = id;
    }

    private Athlete(Person person, string? feiId, Country country, Club club, string category) //TODO: consider Club as persisted across Events (MAUI's raw resources?)
    {
        FeiId = feiId;
		Person = person;
		Country = country;
		Club = club;
        Category = category;
	}

	public string? FeiId { get; private set; }
	public Person Person { get; private set; }
	public Country Country { get; private set; }
	public Club Club { get; private set; }
    public string Category { get; private set; }    

	public override string ToString()
	{
		return $"{Person}, {Club}, {Country}";
	}
}
