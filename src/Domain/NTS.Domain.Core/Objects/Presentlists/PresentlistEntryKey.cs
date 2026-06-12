namespace NTS.Domain.Core.Objects.Presentlists;

public sealed record PresentlistEntryKey
{
    public PresentlistEntryKey(int number, int phaseId, PresentlistEntryType type)
    {
        Number = number;
        PhaseId = phaseId;
        Type = type;
    }

    public int Number { get; }
    public int PhaseId { get; }
    public PresentlistEntryType Type { get; }
}
