namespace NTS.Domain.Setup.Services.Registry;

/// <summary>A row of an Athlete or Horse registry as the matching of a registration sees it: the Tenant's own or another's.</summary>
public sealed record RegistryRow(Guid Id, string Name, string? NameEnglish, string? FeiId);
