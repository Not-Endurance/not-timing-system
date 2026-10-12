using Not.Blazor.Dialogs.Abstractions;
using NTS.Contracts.Features.Console;
using NTS.Domain.Setup.Services.StartValidation;

namespace NoTiming.Console.Features.Home.Dialogs;

/// <summary>
/// What is missing from a Setup for its Event to start, as the domain's start validation says it (#646). A start number that
/// two Competitions of different Phases hold at once is the one thing the person can settle here: they choose the Competition
/// it is taken out of (<see cref="StartCorrections"/>). The Event starts when nothing is left to say.
/// </summary>
public class StartIssuesDialogBehind : NDialog<bool>
{
    readonly Dictionary<int, Guid> _removeFrom = [];

    [Inject]
    IConsoleSetup Setup { get; set; } = default!;

    [Inject]
    IConsoleEvents Events { get; set; } = default!;

    protected bool HasCorrections => Issues.Any(x => x.IsAutoCorrectable);

    protected bool CanApplyCorrections => StartCorrections.CanApply(Issues, _removeFrom);

    [Parameter, EditorRequired]
    public IReadOnlyList<StartValidationIssue> Issues { get; set; } = [];

    [Parameter, EditorRequired]
    public Guid EventId { get; set; }

    protected Guid? RemoveFromOf(int participationNumber)
    {
        return _removeFrom.TryGetValue(participationNumber, out var competition) ? competition : null;
    }

    protected void Choose(int participationNumber, Guid? competition)
    {
        if (competition is { } chosen)
        {
            _removeFrom[participationNumber] = chosen;
        }
        else
        {
            _removeFrom.Remove(participationNumber);
        }
    }

    protected async Task Correct()
    {
        if (!CanApplyCorrections)
        {
            return;
        }

        try
        {
            foreach (var (number, competition) in StartCorrections.Removals(Issues, _removeFrom))
            {
                await Setup.RemoveParticipation(EventId, number, competition);
            }

            Issues = await Events.Validate(EventId);
            _removeFrom.Clear();
            if (Issues.Count == 0)
            {
                await ConfirmDialog(true);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
