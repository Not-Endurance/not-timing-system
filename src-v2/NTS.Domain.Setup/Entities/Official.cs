using NTS.Domain.Setup.Import;
using Newtonsoft.Json;
using static NTS.Domain.Enums.OfficialRole;

namespace NTS.Domain.Setup.Entities;

public class Official : DomainEntity, ISummarizable, IImportable
{
    public static Official Create(string name, OfficialRole role) => new(new Person(name), role);
    public static Official Update(int id, string name, OfficialRole role) => new(id, new Person(name), role);

    [JsonConstructor]
    private Official(int id, Person person, OfficialRole role)
    {
        this.Person = person;
        this.Role = role;
        this.Id = id;
    }
    // TODO: fix ctor usage
    private Official(string name, OfficialRole role)
    {
        if (role == default)
        {
            throw new DomainException(nameof(Role), "Official Role is required");
        }

        this.Person = new Person(name);
		this.Role = role;
    }

    public Person Person { get; private set; }
	public OfficialRole Role { get; private set; }

	public override string ToString()
	{
        return $"{Get(this.Role)}: {this.Person}";
    }
    public bool IsUniqueRole()
    {
        return Role is PresidentVet or PresidentGroundJury or ActiveVet or FeiVetDelegate or FeiTechDelegate or ForeignJudge;
    }
}