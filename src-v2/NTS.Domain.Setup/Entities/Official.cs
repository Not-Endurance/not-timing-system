using NTS.Domain.Setup.Import;
using Newtonsoft.Json;
using static NTS.Domain.Enums.OfficialRole;

namespace NTS.Domain.Setup.Entities;

public class Official : DomainEntity, ISummarizable, IImportable
{
    public static Official Create(string? names, OfficialRole? role) => new(Person.Create(names), role);

    public static Official Update(int id, string? names, OfficialRole? role) => new(id, Person.Create(names), role);

    [JsonConstructor]
    private Official(int id, Person? person, OfficialRole? role) : base(id)
    {
        var name = person;
        this.Role = Required(nameof(Role), role);
        this.Person =  Required(nameof(Person), person);
    }

    private Official(Person? person, OfficialRole? role) : this(GenerateId(), person, role)
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