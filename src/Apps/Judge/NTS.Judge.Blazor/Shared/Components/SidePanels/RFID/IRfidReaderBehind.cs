using Not.Injection;

namespace NTS.Judge.Blazor.Shared.Components.SidePanels.RFID;

public interface IRfidReaderBehind : ISingleton
{
    void StartReading();
    void StopReading();
    bool IsConnected();
}
