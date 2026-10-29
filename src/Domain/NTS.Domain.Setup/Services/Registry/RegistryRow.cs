namespace NTS.Domain.Setup.Services.Registry;

/// <summary>A row of an Athlete or Horse registry as the matching of a registration sees it: the Tenant's own or another's.</summary>
public sealed class RegistryRow
{
    public RegistryRow(Guid id, string name, string? nameEnglish, string? feiId)
    {
        Id = id;
        Name = name;
        NameEnglish = nameEnglish;
        FeiId = feiId;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string? NameEnglish { get; }
    public string? FeiId { get; }
}
