namespace NTS.Domain.Setup.Services.Registry;

/// <summary>A row that an Athlete or a Horse about to be registered is a second copy of, and the reason that decided.</summary>
public sealed class SameMatch
{
    public SameMatch(RegistryRow row, SameReason reason)
    {
        Row = row;
        Reason = reason;
    }

    public RegistryRow Row { get; }
    public SameReason Reason { get; }
}
