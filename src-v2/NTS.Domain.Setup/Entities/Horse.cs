using NTS.Domain.Setup.Import;
using Newtonsoft.Json;

namespace NTS.Domain.Setup.Entities;

public class Horse : DomainEntity, ISummarizable, IImportable
{
    public static Horse Create(string name, string? feiId) => new(name, feiId);

    public static Horse Update(int id, string name, string? feiId) => new(id, name, feiId); 

    [JsonConstructor]
    private Horse(int id, string name, string? feiId) : base(id)
    {
        Name = name;
        FeiId = feiId;
    }
    private Horse(string name, string? feiId) : this(GenerateId(), Required(nameof(Name), name), feiId)
    {
    }

	public string? FeiId { get; }
	public string Name { get; }

	public string Summarize()
	{
        return ToString();
	}
	public override string ToString()
	{
		return Name;
	}
}
