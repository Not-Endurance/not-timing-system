﻿using Not.Notifier;
using NTS.Domain;
using NTS.Domain.Core.Configuration;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Judge.Blazor.Ports;
using NTS.Judge.HardwareControllers;

namespace NTS.Judge.Adapters.Behinds;

public class RfidReaderBehind : IRfidReaderBehind
{
    private readonly ISnapshotProcessor _snapshotProcessor;
    private VupVF747pController VF747PController;
    private Dictionary<int, Timestamp> _deduplication = [];

    public RfidReaderBehind(ISnapshotProcessor snapshotProcessor)
    {
        VF747PController = new VupVF747pController("192.168.68.128", TimeSpan.FromMilliseconds(10));
        _snapshotProcessor = snapshotProcessor;
    }

    public void StartReading()
    {
        if(!VF747PController.IsConnected || !VF747PController.IsReading)
        {
            Task.Run(VF747PController.StartReading);
            VF747PController.OnRead += OnRead;
        }
    }
    public void StopReading()
    {
        VF747PController.StopReading();
        VF747PController.OnRead -= OnRead;
    }

    public async void OnRead(object? sender, (DateTime timestamp, string data) e)
    {
        var number = int.Parse(e.data.Substring(0, 3));
        var timestamp = new Timestamp(e.timestamp);

        if (_deduplication.ContainsKey(number) && _deduplication[number] + TimeSpan.FromSeconds(30) < DateTimeOffset.Now)
        {
            return;
        }
        _deduplication[number] = timestamp;
        var snapshot = new Snapshot(number, StaticOptions.GetRfidSnapshotType(), SnapshotMethod.RFID, timestamp);
        Process(snapshot);
    }

    async void Process(Snapshot snapshot)
    {
        await _snapshotProcessor.Process(snapshot);
        NotifyHelper.Inform("Processed: " + snapshot.ToString());
    }

    public bool IsConnected()
    {
        return VF747PController.IsReading && VF747PController.IsConnected;
    }
}
