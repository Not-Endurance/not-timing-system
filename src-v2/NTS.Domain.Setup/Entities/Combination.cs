using NTS.Domain.Setup.Import;
using Newtonsoft.Json;

namespace NTS.Domain.Setup.Entities;

public class Combination : DomainEntity, ISummarizable, IImportable, IParent
{
    public static Combination Create(int number, Athlete? athlete, Horse? horse) => new(number, athlete, horse);

    public static Combination Update(int id, int number, Athlete? athlete, Horse? horse) => new(id, number, athlete, horse);

    [JsonConstructor]
    public Combination(int id, int number, Athlete? athlete, Horse? horse) : this(number, athlete, horse)
    {
        Id = id;
    }
    public Combination(int number, Athlete? athlete, Horse? horse)
    {
        if (number == default)
        {
            throw new DomainException(nameof(Number), "Combination Number is required");
        }

        Number = number;
        Athlete = athlete ?? throw new DomainException(nameof(Athlete), "Athlete is required");
        Horse = horse ?? throw new DomainException(nameof(Horse), "Horse is required");
    }

    public int Number { get; private set; }
    public Athlete Athlete { get; private set; }
    public Horse Horse { get; private set; }

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
