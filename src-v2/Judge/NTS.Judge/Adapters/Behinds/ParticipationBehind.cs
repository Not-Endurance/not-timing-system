using Not.Application.Adapters.Behinds;
using Not.Application.Ports.CRUD;
using Not.Exceptions;
using Not.Safe;
using NTS.Domain.Core.Entities.ParticipationAggregate;
using NTS.Domain.Core.Entities;
using NTS.Domain.Objects;
using NTS.Judge.Blazor.Ports;
using Not.Blazor.Ports.Behinds;
using NTS.Judge.Blazor.Pages.Dashboard.Phases;

namespace NTS.Judge.Adapters.Behinds;

public class ParticipationBehind : ObservableBehind, IParticipationBehind, IUpdateBehind<PhaseUpdateModel>
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
    protected override async Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        Participations = await _participationRepository.ReadAll();
        SelectedParticipation = Participations.FirstOrDefault();
        return Participations.Any();
    }

    async Task SafeRequestReinspection(bool requestFlag)
    {
        SelectedParticipation!.ChangeReinspection(requestFlag);
        await _participationRepository.Update(SelectedParticipation);

        EmitChange();
    }

    public async Task<PhaseUpdateModel> Update(PhaseUpdateModel model)
    {
        //probably should use Participations collection to ensure a single point of truth is used for the data
        var participation = Participations.FirstOrDefault(x => x.Phases.Any(y => y.Id == model.Id));
        GuardHelper.ThrowIfDefault(participation);

        participation.Update(model);
        await _participationRepository.Update(participation);
        EmitChange();
        return model;
    }

    async Task SafeRequestRequiredInspection(bool requestFlag)
    {
        SelectedParticipation!.ChangeRequiredInspection(requestFlag);
        await _participationRepository.Update(SelectedParticipation);

        EmitChange();
    }

    async Task SafeProcess(Snapshot snapshot)
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

    async Task SafeWithdraw()
    {
        GuardHelper.ThrowIfDefault(SelectedParticipation);
        SelectedParticipation.Withdraw();
        await _participationRepository.Update(SelectedParticipation);

        EmitChange();
    }

    async Task SafeRetire()
    {
        GuardHelper.ThrowIfDefault(SelectedParticipation);
        SelectedParticipation.Retire();
        await _participationRepository.Update(SelectedParticipation);

        EmitChange();
    }

    async Task SafeFinishNotRanked(string reason)
    {
        GuardHelper.ThrowIfDefault(SelectedParticipation);
        SelectedParticipation.FinishNotRanked(reason);
        await _participationRepository.Update(SelectedParticipation);

        EmitChange();
    }

    async Task SafeDisqualify(string reason)
    {
        GuardHelper.ThrowIfDefault(SelectedParticipation);
        SelectedParticipation.Disqualify(reason);
        await _participationRepository.Update(SelectedParticipation);

        EmitChange();
    }

    async Task SafeFailToQualify(FTQCodes[] ftqCodes, string? reason)
    {
        GuardHelper.ThrowIfDefault(SelectedParticipation);

        SelectedParticipation.FailToQualify(ftqCodes, reason);
        await _participationRepository.Update(SelectedParticipation);

        EmitChange();
    }

    async Task SafeRestoreQualification()
    {
        GuardHelper.ThrowIfDefault(SelectedParticipation);
        SelectedParticipation.Restore();
        await _participationRepository.Update(SelectedParticipation);
        
        EmitChange();
    }

    #region SafePattern

    Participation? SafeGet(int id)
    {
        return Participations.FirstOrDefault(x => x.Id == id);
    }

    public async Task Process(Snapshot snapshot)
    {
        await SafeHelper.Run(() => SafeProcess(snapshot));
    }

    public async Task RequestRepresent(bool isRequested)
    {
        await SafeHelper.Run(() => SafeRequestReinspection(isRequested));
    }

    public async Task RequireInspection(bool isRequested)
    {
        await SafeHelper.Run(() => SafeRequestRequiredInspection(isRequested));
    }

    public async Task Withdraw()
    {
        await SafeHelper.Run(SafeWithdraw);
    }

    public async Task Retire()
    {
        await SafeHelper.Run(SafeRetire);
    }

    public async Task FinishNotRanked(string reason)
    {
        await SafeHelper.Run(() => SafeFinishNotRanked(reason));
    }

    public async Task Disqualify(string reason)
    {
        await SafeHelper.Run(() => SafeDisqualify(reason));
    }

    public async Task FailToQualify(FTQCodes[] ftqCodes, string? reason)
    {
        await SafeHelper.Run(() => SafeFailToQualify(ftqCodes, reason));
    }

    public async Task RestoreQualification()
    {
        await SafeHelper.Run(SafeRestoreQualification);
    }

    public Participation? Get(int id)
    {
        return SafeHelper.Run(() => SafeGet(id));
    }

    #endregion
}