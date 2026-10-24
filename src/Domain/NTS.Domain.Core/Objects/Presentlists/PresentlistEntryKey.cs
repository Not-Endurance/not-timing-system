namespace NTS.Domain.Core.Objects.Presentlists;

public sealed record PresentlistEntryKey
{
    public PresentlistEntryKey(int number, Guid phaseId, PresentlistEntryType type)
    {
        Number = number;
        PhaseId = phaseId;
        Type = type;
    }

    public int Number { get; }
    public Guid PhaseId { get; }
    public PresentlistEntryType Type { get; }
}
