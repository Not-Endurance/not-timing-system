using Not.Storage.JsonFile.States;

namespace NTS.Storage.Setup;

public class SetupState : NState
{
    public int? ConnectedEventId { get; set; }
}
