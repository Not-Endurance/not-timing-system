using NTS.Domain.Setup.Entities;

namespace NTS.Persistence.Setup;

public class SetupState : NotState, ITreeState<Event>
{
    public Event? Event { get; set; }
    
    Event? ITreeState<Event>.Root
    {
        get => Event; 
        set => Event = value;
    }
}