using Not.Blazor.Dialogs.Abstractions;
using Not.Structures;
using NTS.Domain.Setup.Services.StartValidation;
using NTS.Judge.Contracts.Features.Setup.UpcomingEvents;

namespace NTS.Judge.Blazor.Features.Setup.StartValidation;

public class StartValidationDialogBehind : NDialog<bool>
{
    readonly Dictionary<int, int> _selectedCompetitionByParticipation = [];

    [Inject]
    IUpcomingEventService UpcomingEventService { get; set; } = default!;

    protected Result<IReadOnlyList<StartValidationIssue>> Validation { get; set; } =
        Result.Success<IReadOnlyList<StartValidationIssue>>([]);

    protected IReadOnlyList<StartValidationIssue> Issues => Validation.Data ?? [];
    protected bool HasIssues => Issues.Any();
    protected bool CanApplyCorrections =>
        HasIssues
        && Issues.All(issue => issue.IsAutoCorrectable)
        && Issues.All(issue =>
            issue.ParticipationNumber.HasValue
            && _selectedCompetitionByParticipation.ContainsKey(issue.ParticipationNumber.Value)
        );

    [Parameter]
    public Result<IReadOnlyList<StartValidationIssue>> InitialValidation { get; set; } =
        Result.Success<IReadOnlyList<StartValidationIssue>>([]);

    [Parameter]
    public int UpcomingEventId { get; set; }

    protected override void OnParametersSet()
    {
        Validation = InitialValidation;
        SyncSelectionsWithCurrentIssues();
    }

    protected int? GetSelectedCompetition(int participationNumber)
    {
        if (_selectedCompetitionByParticipation.TryGetValue(participationNumber, out var selectedCompetition))
        {
            return selectedCompetition;
        }
        return null;
    }

    protected void SetSelectedCompetition(int participationNumber, int? competitionId)
    {
        if (!competitionId.HasValue)
        {
            _selectedCompetitionByParticipation.Remove(participationNumber);
            return;
        }
        _selectedCompetitionByParticipation[participationNumber] = competitionId.Value;
    }

    protected async Task Start()
    {
        if (!CanApplyCorrections)
        {
            return;
        }

        try
        {
            foreach (var issue in Issues)
            {
                if (!issue.ParticipationNumber.HasValue)
                {
                    continue;
                }

                var participationNumber = issue.ParticipationNumber.Value;
                var selectedCompetitionId = _selectedCompetitionByParticipation[participationNumber];
                foreach (var competition in issue.Competitions.Where(x => x.CompetitionId != selectedCompetitionId))
                {
                    await UpcomingEventService.DeleteParticipation(
                        UpcomingEventId,
                        participationNumber,
                        competition.CompetitionId
                    );
                }
            }

            Validation = await UpcomingEventService.Validate(UpcomingEventId);
            if (HasIssues)
            {
                SyncSelectionsWithCurrentIssues();
                return;
            }
            await ConfirmDialog(true);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    void SyncSelectionsWithCurrentIssues()
    {
        var activeParticipations = Issues
            .Where(x => x.ParticipationNumber.HasValue)
            .Select(x => x.ParticipationNumber!.Value)
            .ToHashSet();
        var staleSelections = _selectedCompetitionByParticipation
            .Keys.Where(participationNumber => !activeParticipations.Contains(participationNumber))
            .ToList();
        foreach (var participationNumber in staleSelections)
        {
            _selectedCompetitionByParticipation.Remove(participationNumber);
        }
    }
}
