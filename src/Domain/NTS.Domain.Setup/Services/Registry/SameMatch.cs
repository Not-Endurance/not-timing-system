namespace NTS.Domain.Setup.Services.Registry;

/// <summary>A row that an Athlete or a Horse about to be registered is a second copy of, and the reason that decided.</summary>
public sealed record SameMatch(RegistryRow Row, SameReason Reason);
