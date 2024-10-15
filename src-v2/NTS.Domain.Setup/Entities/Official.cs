using NTS.Domain.Setup.Import;
using Newtonsoft.Json;
using static NTS.Domain.Enums.OfficialRole;

namespace NTS.Domain.Setup.Entities;

public class Official : DomainEntity, ISummarizable, IImportable
{
    public static Official Create(string? names, OfficialRole? role) => new(RequiredPerson(names), role);

    public static Official Update(int id, string? names, OfficialRole? role) => new(id, RequiredPerson(names), role);

    static Person RequiredPerson(string? names)
    {
        var required = Required(nameof(Person), names);
        return new Person(required);
    }

    [JsonConstructor]
    private Official(int id, Person person, OfficialRole? role) : base(id)
    {
        var name = person;
        this.Role = Required(nameof(Role), role);
        this.Person =  new Person(name);
    }

    private Official(Person person, OfficialRole? role) : this(GenerateId(), person, role)
    {
    }

    public Person Person { get; private set; }
	public OfficialRole Role { get; private set; }

	public override string ToString()
	{
        return Combine(Role.GetDescription(), Person);
    }

    public bool IsUniqueRole()
    {
        return Role is VeterinaryCommissionPresident or GroundJuryPresident or  ForeignVeterinaryDelegate or TechnicalDelegate or ForeignJudge;
    }
}