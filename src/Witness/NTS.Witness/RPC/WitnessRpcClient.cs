using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Not.Application.RPC;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Startlists;
using NTS.Domain.Watcher;
using NTS.Witness;
using NTS.Witness.RPC.Procedures;

namespace NTS.Witness.RPC;

public class WitnessRpcClient
    : RpcClient,
        IWitnessClientProcedures,
        IWitnessParticipantsHubProcedures,
        IWitnessSnapshotsHubProcedures,
        IWitnessStartlistHubProcedures
{
    const int DEFAULT_SNAPSHOT_PARTICIPANTS = 10;

    readonly IRpcSocket _socket;
    readonly Dictionary<int, WitnessStartlist> _startlists;
    readonly List<Participation> _participants;
    readonly List<IntermediateSnapshot> _snapshots;

    public WitnessRpcClient(IRpcSocket socket)
        : base(socket)
    {
        _socket = socket;
        _startlists = new();
        _participants = new();
        _snapshots = new();
    }

    public IReadOnlyDictionary<int, WitnessStartlist> Startlists =>
        new ReadOnlyDictionary<int, WitnessStartlist>(_startlists);

    public IReadOnlyCollection<Participation> Participants => _participants.AsReadOnly();

    public IReadOnlyCollection<IntermediateSnapshot> Snapshots => _snapshots.AsReadOnly();

    public override void RunAtStartup()
    {
        RegisterInputProcedure<StartlistEntry, WitnessCollectionAction>(nameof(ReceiveEntry), ReceiveEntry);
        RegisterInputProcedure<Participation, WitnessCollectionAction>(nameof(ReceiveEntryUpdate), ReceiveEntryUpdate);
        RegisterInputProcedure<WitnessSnapshotPayload>(nameof(ReceiveWitnessEvent), ReceiveWitnessEvent);

        RegisterOutputProcedure(nameof(SendStartlist), SendStartlist);
        RegisterOutputCollectionProcedure(nameof(SendParticipants), SendParticipants);

        EnsureInitialized();
    }

    public void EnsureInitialized()
    {
        EnsureStartlistsLoaded();
        EnsureParticipantsLoaded();
    }

    public async Task<RpcInvokeResult> PublishSnapshotsAsync(WitnessSnapshotPayload payload)
    {
        return await _socket.InvokeInputProcedure(nameof(ReceiveWitnessEvent), payload);
    }

    public async Task<RpcInvokeResult<Dictionary<int, WitnessStartlist>>> RequestStartlistAsync()
    {
        return await _socket.InvokeOutputProcedure<Dictionary<int, WitnessStartlist>>(nameof(SendStartlist));
    }

    public async Task<RpcInvokeResult<IEnumerable<Participation>>> RequestParticipantsAsync()
    {
        return await _socket.InvokeOutputProcedure<IEnumerable<Participation>>(nameof(SendParticipants));
    }

    public Task ReceiveEntry(StartlistEntry entry, WitnessCollectionAction action)
    {
        var startlist = GetOrCreateStartlist(entry.PhaseNumber);
        startlist.Update(entry, action);
        return Task.CompletedTask;
    }

    public Task ReceiveEntryUpdate(Participation participation, WitnessCollectionAction action)
    {
        UpdateCollection(_participants, participation, action, p => p.Id);
        return Task.CompletedTask;
    }

    public Task ReceiveWitnessEvent(WitnessSnapshotPayload payload)
    {
        if (payload?.Entries == null)
        {
            return Task.CompletedTask;
        }

        foreach (var snapshot in payload.Entries)
        {
            UpdateCollection(_snapshots, snapshot, WitnessCollectionAction.AddOrUpdate, s => s.Number);
        }

        return Task.CompletedTask;
    }

    public Task<Dictionary<int, WitnessStartlist>> SendStartlist()
    {
        EnsureStartlistsLoaded();
        var copy = _startlists.ToDictionary(x => x.Key, x => new WitnessStartlist(x.Value));
        return Task.FromResult(copy);
    }

    public Task<IEnumerable<Participation>> SendParticipants()
    {
        EnsureParticipantsLoaded();
        return Task.FromResult<IEnumerable<Participation>>(_participants.ToList());
    }

    void EnsureStartlistsLoaded()
    {
        if (_startlists.Count > 0)
        {
            return;
        }

        var participations = DummyData.CreateParticipationsForStartlist();
        var startList = new StartList(participations, () => { });
        var entries = startList.Upcoming.Concat(startList.History);
        foreach (var group in entries.GroupBy(entry => entry.PhaseNumber))
        {
            _startlists[group.Key] = new WitnessStartlist(group);
        }
    }

    void EnsureParticipantsLoaded()
    {
        if (_participants.Count > 0)
        {
            return;
        }

        var participations = DummyData.CreateParticipationsForSnapshot(DEFAULT_SNAPSHOT_PARTICIPANTS);
        _participants.AddRange(participations);
    }

    WitnessStartlist GetOrCreateStartlist(int phaseNumber)
    {
        if (!_startlists.TryGetValue(phaseNumber, out var startlist))
        {
            startlist = new WitnessStartlist();
            _startlists[phaseNumber] = startlist;
        }

        return startlist;
    }

    static void UpdateCollection<T, TKey>(
        List<T> collection,
        T item,
        WitnessCollectionAction action,
        Func<T, TKey> keySelector
    )
        where TKey : notnull
    {
        var comparer = EqualityComparer<TKey>.Default;
        var key = keySelector(item);
        var index = collection.FindIndex(existing => comparer.Equals(keySelector(existing), key));

        if (index >= 0)
        {
            collection.RemoveAt(index);
        }

        if (action == WitnessCollectionAction.AddOrUpdate)
        {
            collection.Add(item);
        }
    }
}
