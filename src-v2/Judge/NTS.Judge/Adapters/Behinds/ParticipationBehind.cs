﻿using Not.Application.Ports.CRUD;
using Not.Blazor.Ports.Behinds;
using Not.Exceptions;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Objects;
using NTS.Judge.Blazor.Ports;

namespace NTS.Judge.Adapters.Behinds;

public class ParticipationBehind : ObservableBehind, IParticipationBehind
{
    private readonly IRepository<Participation> _participationRepository;
    private readonly IRepository<SnapshotResult> _snapshotResultRepository;
    private Participation? _selectedParticipation;

    public ParticipationBehind(
        IRepository<Participation> participationRepository,
        IRepository<SnapshotResult> snapshotResultRepository)
    {
        _participationRepository = participationRepository;
        _snapshotResultRepository = snapshotResultRepository;
    }

    public IEnumerable<Participation> Participations { get; private set; } = new List<Participation>();
    public IEnumerable<IGrouping<double, Participation>> ParticipationsByDistance => Participations.GroupBy(x => x.Phases.Distance);
    public Participation? SelectedParticipation
    {
        get => _selectedParticipation; 
        set
        {
            _selectedParticipation = value;
            EmitChange();
        } 
    }

    // TODO: we need a better solution to load items as they have been changed in addition to load on startup.
    // Example case: importing previous data: as it is currently we have to restart the app after import
    // Maybe some sort of observable repositories?
    protected override async Task<bool> PerformInitialization()
    {
        Participations = await _participationRepository.ReadAll();
        SelectedParticipation = Participations.FirstOrDefault();
        return Participations.Any();
    }

    public void RequestReinspection(bool requestFlag)
    {
        SelectedParticipation!.ChangeReinspection(requestFlag);
        _participationRepository.Update(SelectedParticipation);

        EmitChange();
    }

    public void RequestRequiredInspection(bool requestFlag)
    {
        SelectedParticipation!.ChangeRequiredInspection(requestFlag);
        _participationRepository.Update(SelectedParticipation);

        EmitChange();
    }

    public async Task Process(Snapshot snapshot)
    {
        var participation = Participations.FirstOrDefault(x => x.Tandem.Number == snapshot.Number);
        if (participation == null)
        {
            return;
        }
        var result = participation.Process(snapshot);
        if (result.Type == SnapshotResultType.Applied)
        {
            await _participationRepository.Update(participation);
        }
        await _snapshotResultRepository.Create(result);

        EmitChange();
    }

    public async Task Update(IPhaseState state)
    {
        //probably should use Participations collection to ensure a single point of truth is used for the data
        var participation = await _participationRepository.Read(x => x.Phases.Any(y => y.Id == state.Id));
        GuardHelper.ThrowIfDefault(participation);

        participation.Update(state);
        await _participationRepository.Update(participation);
    }

    public async Task Withdraw()
    {
        var tandemNumber = SelectedParticipation!.Tandem.Number;
        var participation = Participations.FirstOrDefault(x => x.Tandem.Number == tandemNumber);
        GuardHelper.ThrowIfDefault(participation);
        participation.Withdraw();
        await _participationRepository.Update(participation);

        EmitChange();
    }

    public async Task Retire()
    {
        var tandemNumber = SelectedParticipation!.Tandem.Number;
        var participation = Participations.FirstOrDefault(x => x.Tandem.Number == tandemNumber);
        GuardHelper.ThrowIfDefault(participation);
        participation.Retire();
        await _participationRepository.Update(participation);

        EmitChange();
    }

    public async Task FinishNotRanked(string reason)
    {
        var tandemNumber = SelectedParticipation!.Tandem.Number;
        var participation = Participations.FirstOrDefault(x => x.Tandem.Number == tandemNumber);
        GuardHelper.ThrowIfDefault(participation);
        participation.FinishNotRanked(reason);
        await _participationRepository.Update(participation);

        EmitChange();
    }

    public async Task Disqualify(string reason)
    {
        var tandemNumber = SelectedParticipation!.Tandem.Number;
        var participation = Participations.FirstOrDefault(x => x.Tandem.Number == tandemNumber);
        GuardHelper.ThrowIfDefault(participation);
        participation.Disqualify(reason);
        await _participationRepository.Update(participation);

        EmitChange();
    }

    public async Task FailToQualify(params FTQCodes[] ftqCodes)
    {
        var tandemNumber = SelectedParticipation!.Tandem.Number;
        var participation = Participations.FirstOrDefault(x => x.Tandem.Number == tandemNumber);
        GuardHelper.ThrowIfDefault(participation);
        GuardHelper.ThrowIfDefault(ftqCodes);
        participation.FailToQualify(ftqCodes);
        await _participationRepository.Update(participation);

        EmitChange();
    }

    public async Task FailToQualify(string? reason, params FTQCodes[] ftqCodes)
    {
        var tandemNumber = SelectedParticipation!.Tandem.Number;
        var participation = Participations.FirstOrDefault(x => x.Tandem.Number == tandemNumber);
        GuardHelper.ThrowIfDefault(participation);
        GuardHelper.ThrowIfDefault(ftqCodes);
        participation.FailToQualify(reason, ftqCodes);
        await _participationRepository.Update(participation);

        EmitChange();
    }

    public async Task RestoreQualification()
    {
        var tandemNumber = SelectedParticipation!.Tandem.Number;
        var participation = Participations.FirstOrDefault(x => x.Tandem.Number == tandemNumber);
        GuardHelper.ThrowIfDefault(participation);

        participation.RestoreQualification();
        await _participationRepository.Update(participation);
        EmitChange();
    }

    public async Task<Participation?> Get(int id)
    {
        return await _participationRepository.Read(id);
    }
}