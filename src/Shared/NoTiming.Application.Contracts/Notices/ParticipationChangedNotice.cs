using NC.Application.RPC.Changes;

namespace NoTiming.Application.Contracts.Notices;

/// <summary>Tells clients that a participation changed. It carries only the ID; clients fetch the state themselves.</summary>
public class ParticipationChangedNotice : ChangeNotice
{
    public int ParticipationId { get; set; }
}
