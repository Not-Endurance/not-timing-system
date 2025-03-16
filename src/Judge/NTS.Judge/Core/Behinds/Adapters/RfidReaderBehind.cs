using Not.Notify;
using Not.Safe;
using NTS.Domain.Core.StaticOptions;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Judge.Blazor.Shared.Components.SidePanels.RFID;
using NTS.Judge.RFID;

namespace NTS.Judge.Core.Behinds.Adapters;

public class RfidReaderBehind : IRfidReaderBehind
{
    readonly ISnapshotProcessor _snapshotProcessor;
    VupVF747pController _vF747PController;
    Dictionary<int, Timestamp> _deduplication = [];

    public RfidReaderBehind(ISnapshotProcessor snapshotProcessor)
    {
        _vF747PController = new VupVF747pController("192.168.68.128", TimeSpan.FromMilliseconds(10));
        _snapshotProcessor = snapshotProcessor;
    }

    public void StartReading()
    {
        SafeHelper.Run(SafeStartReading);
    }

    public void StopReading()
    {
        SafeHelper.Run(SafeStopReading);
    }

    public void OnRead(object? sender, (DateTime timestamp, string data) e)
    {
        void action() => SafeOnRead(e);
        SafeHelper.Run(action);
    }

    public bool IsConnected()
    {
        return SafeHelper.Run(SafeIsConnected);
    }

    public async void Process(Snapshot snapshot)
    {
        Task action() => SafeProcess(snapshot);
        await SafeHelper.RunAsync(action);
    }

    void SafeStartReading()
    {
        if (!_vF747PController.IsConnected || !_vF747PController.IsReading)
        {
            Task.Run(_vF747PController.StartReading);
            _vF747PController.OnRead += OnRead;
        }
    }

    void SafeStopReading()
    {
        _vF747PController.StopReading();
        _vF747PController.OnRead -= OnRead;
    }

    void SafeOnRead((DateTime timestamp, string data) e)
    {
        var s = e.data.Substring(0, 3);
        var number = int.Parse(s);
        var timestamp = new Timestamp(e.timestamp);

        if (
            _deduplication.ContainsKey(number)
            && _deduplication[number] + TimeSpan.FromSeconds(30) < DateTimeOffset.Now
        )
        {
            return;
        }
        _deduplication[number] = timestamp;
        var snapshot = new Snapshot(number, StaticOption.GetRfidSnapshotType(), SnapshotMethod.RFID, timestamp);
        Process(snapshot);
    }

    bool SafeIsConnected()
    {
        return _vF747PController.IsReading && _vF747PController.IsConnected;
    }

    async Task SafeProcess(Snapshot snapshot)
    {
        await _snapshotProcessor.Process(snapshot);
        var message = "Processed: " + snapshot.ToString();
        NotifyHelper.Inform(message);
    }
}
