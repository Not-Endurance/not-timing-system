using Not.Storage.Ports.States;
using NTS.Domain.Setup.Entities;

namespace NTS.Persistence.Setup;

public class SetupState : ITreeState<Event>, ISetState<Loop>, ISetState<Athlete>
{
    public Event? Event { get; set; }
    public List<Loop> Loops { get; } = new List<Loop>();

    public List<Athlete> Athletes { get; } = new List<Athlete>();

    Event? ITreeState<Event>.Root
    {
        get => Event; 
        set => Event = value;
    }

    List<Loop> ISetState<Loop>.EntitySet => Loops;
    List<Athlete> ISetState<Athlete>.EntitySet => Athletes;

}