using Core.Application.Services;
using Core.ConventionalServices;
using Core.Domain.AggregateRoots.Manager;
using Core.Domain.AggregateRoots.Manager.Aggregates.Participants;
using Core.Enums;
using Core.Models;
using EMS.Witness.Rpc;
using EMS.Witness.Shared.Toasts;

namespace EMS.Witness.Services;

public class ParticipantsService : IParticipantsService
{
	private readonly IPersistenceService _persistence;
	private readonly IWitnessState state;
    private readonly IWitnessContext context;
    private readonly IParticipantsClient participantsClient;
    private readonly IToaster toaster;
    private readonly IDateService dateService;

    public ParticipantsService(
        IPersistenceService persistence,
        IWitnessState state,
        IWitnessContext context,
        IParticipantsClient arrivelistClient,
        IToaster toaster,
        IDateService dateService)
    {
		_persistence = persistence;
		this.state = state;
        this.context = context;
        this.participantsClient = arrivelistClient;
        this.toaster = toaster;
        this.dateService = dateService;
    }

    public SortedCollection<ParticipantEntry> Participants => this.context.Participants;
    public ObservableCollection<ParticipantEntry> Snapshots => this.state.ParticipantSnapshots;
    public ObservableCollection<ParticipantEntry> Selected => this.state.ParticipantSelected;
    public SortedCollection<ParticipantsBatch> History => this.state.ParticipantHistory;

    public void EditSnapshot(string number, DateTime time)
    {
        var entry = this.Snapshots.FirstOrDefault(x => x.Number == number);
        if (entry is null)
        {
            this.toaster.Add("Not found", $"Entry with number '{number}' not found.", UiColor.Warning);
            return;
        }
        entry.ArriveTime = time;
    }

    public async Task Load()
    {
        var result = await this.participantsClient.Load();
        if (result.IsSuccessful)
        {
            //var (eventId, participants) = result.Data;
            var eventId = result.Data!.EventId;
            var participants = result.Data.Participants;
            if (eventId == default || participants == default)
            {
                return;
            }
			this.Participants.Clear();
			this.Participants.AddRange(participants!);
            if (!state.EventId.HasValue)
            {
                state.EventId = eventId;
                await _persistence.RestoreIfAny(eventId);
            }
		}
    }

    public void Sort(bool byNumber = false, bool byDistance = false, bool byName = false)
    {
        if (byNumber)
        {
            this.Participants.Sort((a, b) => int.Parse(a.Number).CompareTo(int.Parse(b.Number)));
        }
        else if (byDistance)
        {
            this.Participants.Sort((a, b) => a.LapDistance.CompareTo(b.LapDistance));
        }
        else if (byName)
        {
            this.Participants.Sort((a, b) => a.Name.CompareTo(b.Name));
        }
    }

    public void RemoveSnapshot(ParticipantEntry entry)
    {
        entry.ArriveTime = null;
        this.Snapshots.Remove(entry);
        this.Participants.Add(entry);
    }

    public void RemoveSelected(ParticipantEntry entry)
    {
        this.Selected.Remove(entry);
    }

    public async Task Send(WitnessEventType type)
    {
        var result = await this.participantsClient.Send(this.Snapshots, type);
        if (result.IsSuccessful)
        {
            var batch = new ParticipantsBatch(type, this.Snapshots);
            this.History.Add(batch);
            this.Snapshots.Clear();
        }
    }
    public async Task Resend(ParticipantsBatch batch, WitnessEventType type)
    {
        var result = await this.participantsClient.Send(batch.Participants, type);
        if (result.IsSuccessful)
        {
            this.toaster.Add($"{nameof(this.Resend)} Successful", $"Resent '{batch.Participants.Count}' entries", UiColor.Success, 3);
        }
    }

    public void Select(ParticipantEntry entry)
    {
        if (!this.Selected.Contains(entry))
        {
            this.Selected.Add(entry);
        }
    }

    public void CreateSnapshot(ParticipantEntry entry)
    {
        this.Selected.Remove(entry);

        entry.ArriveTime = DateTime.Now;
        var existing = this.Snapshots.FirstOrDefault(x => x == entry);
        if (existing != null)
        {
            this.Snapshots.Remove(existing);
        }
        this.Snapshots.Add(entry);
    }

    public void Update(ParticipantEntry entry, CollectionAction action)
    {
        this.Participants.Update(entry, action);
    }
}

public interface IParticipantsService : ISingletonService
{
    SortedCollection<ParticipantEntry> Participants { get; }
    ObservableCollection<ParticipantEntry> Selected { get; }
    ObservableCollection<ParticipantEntry> Snapshots { get; }
    SortedCollection<ParticipantsBatch> History { get; }
    Task Load();
    void Sort(bool byNumber = false, bool byDistance = false, bool byName = false);
    void Update(ParticipantEntry entry, CollectionAction action);
    void Select(ParticipantEntry entry);
    void RemoveSelected(ParticipantEntry entry);
    void EditSnapshot(string number, DateTime time);
    void CreateSnapshot(ParticipantEntry entry);
    void RemoveSnapshot(ParticipantEntry entry);
    Task Send(WitnessEventType type);
    Task Resend(ParticipantsBatch batch, WitnessEventType type);
}