using Not.Notify;

namespace NTS.Judge.Blazor.Setup.Combinations.RFID;

public partial class RfidTagWriter
{
    bool _awaitingScan = false;

    [Inject]
    public IRfidWriterBehind TagBehind { get; set; } = default!;

    [Parameter]
    public int CombinationNumber { get; set; }

    public async Task AddTag()
    {
        _awaitingScan = true;
        NotifyHelper.Inform("Waiting for Tag Scan");
        Value = await TagBehind.WriteTag(CombinationNumber);
        _awaitingScan = false;
    }

    public void RemoveTag()
    {
        Value = null;
    }
}
