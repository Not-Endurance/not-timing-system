namespace NTS.Judge.Blazor.Shared.Components.SidePanels.RFID;

public partial class RfidPanel
{
    [Inject]
    IRfidReaderBehind ReaderBehind { get; set; } = default!;

    [Parameter]
    public bool EnduranceEventStarted { get; set; } = default!;

    protected override void OnParametersSet()
    {
        if (EnduranceEventStarted)
        {
            ReaderBehind.StartReading();
            Thread.Sleep(5000);
            InvokeAsync(StateHasChanged);
        }
    }

    public void Reconnect()
    {
        ReaderBehind.StartReading();
        Thread.Sleep(1000);
        InvokeAsync(StateHasChanged);
    }

    public void StopDetecting()
    {
        ReaderBehind.StopReading();
        Thread.Sleep(1000);
        InvokeAsync(StateHasChanged);
    }
}
