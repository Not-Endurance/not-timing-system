using Not.Storage.States;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.Setup;

public class SetupState : NState, ITreeState<UpcomingEvent>
{
    UpcomingEvent? ITreeState<UpcomingEvent>.Root
    {
        get => EnduranceEvent;
        set => EnduranceEvent = value;
    }

    public UpcomingEvent? EnduranceEvent { get; set; }
}
