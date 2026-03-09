using Not.Blazor.Dialogs.Abstractions;
using Not.Structures;
using NTS.Domain.Setup.Services.StartValidation;
using NTS.Judge.Features;

namespace NTS.Judge.Blazor.Features.Setup.StartValidation;

public class StartValidationDialogBehind : NDialog<bool>
{
    readonly Dictionary<int, int> _selectedCompetitionByParticipation = [];

    [Inject]
    IStartBusiness StartService { get; set; } = default!;

    protected Result<IReadOnlyList<StartValidationIssue>> Validation { get; set; } =
        Result.Success<IReadOnlyList<StartValidationIssue>>([]);

    protected IReadOnlyList<StartValidationIssue> Issues => Validation.Data ?? [];
    protected bool HasIssues => Issues.Any();
    protected bool CanApplyCorrections =>
        HasIssues
        && Issues.All(issue => _selectedCompetitionByParticipation.ContainsKey(issue.ParticipationNumber));

    [Parameter]
    public Result<IReadOnlyList<StartValidationIssue>> InitialValidation { get; set; } =
        Result.Success<IReadOnlyList<StartValidationIssue>>([]);

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
                var selectedCompetitionId = _selectedCompetitionByParticipation[issue.ParticipationNumber];
                foreach (var competition in issue.Competitions.Where(x => x.CompetitionId != selectedCompetitionId))
                {
                    await StartService.DeleteParticipation(issue.ParticipationNumber, competition.CompetitionId);
                }
            }

            Validation = StartService.Validate();
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
        var activeParticipations = Issues.Select(x => x.ParticipationNumber).ToHashSet();
        var staleSelections = _selectedCompetitionByParticipation.Keys
            .Where(participationNumber => !activeParticipations.Contains(participationNumber))
            .ToList();
        foreach (var participationNumber in staleSelections)
        {
            _selectedCompetitionByParticipation.Remove(participationNumber);
        }
    }
}
