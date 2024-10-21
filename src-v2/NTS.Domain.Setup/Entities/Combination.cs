using NTS.Domain.Setup.Import;
using Newtonsoft.Json;

namespace NTS.Domain.Setup.Entities;

public class Combination : DomainEntity, ISummarizable, IImportable, IParent
{
    public static Combination Create(int number, Athlete? athlete, Horse? horse, Tag? tag) => new(number, athlete, horse, tag);
    public static Combination Update(int id, int number, Athlete? athlete, Horse? horse, Tag? tag) => new(id, number, athlete, horse, tag);

    [JsonConstructor]
    public Combination(int id, int number, Athlete? athlete, Horse? horse, Tag? tag)
    {
        Id = id;
        Number = NotDefault(nameof(Number), number);
        Athlete = Required(nameof(Athlete), athlete);
        Horse = Required(nameof(Horse), horse);
        Tag = tag;
    }
    public Combination(int number, Athlete? athlete, Horse? horse, Tag? tag) : this(GenerateId(), number, athlete, horse, tag)
    {
    }

    public int Number { get; private set; }
    public Athlete Athlete { get; private set; }
    public Horse Horse { get; private set; }
    public Tag? Tag { get; private set; }

    public string Summarize()
    {
        var summary = new Summarizer(this);
        return summary.ToString();
    }

	public override string ToString()
	{
        return Combine($"{"#".Localize()}{Number}", Athlete, Horse);
	}
}
