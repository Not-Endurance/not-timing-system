using Not.Blazor.Dialogs.Abstractions;
using NTS.Contracts.Features.Console;
using NTS.Domain.Setup.Services.StartValidation;

namespace NoTiming.Console.Features.Home.Dialogs;

/// <summary>
/// What is missing from a Setup for its Event to start, as the domain's start validation says it (#646). A start number that
/// two Competitions of different Phases hold at once is the one thing the person can settle here: they choose the Competition
/// it stays in, and it is taken out of the others. The Event starts when nothing is left to say.
/// </summary>
public class StartIssuesDialogBehind : NDialog<bool>
{
    readonly Dictionary<int, Guid> _stays = [];

    [Inject]
    IConsoleSetup Setup { get; set; } = default!;

    [Inject]
    IConsoleEvents Events { get; set; } = default!;

    protected bool HasCorrections => Issues.Any(x => x.IsAutoCorrectable);

    protected bool CanApplyCorrections =>
        Issues.Count > 0
        && Issues.All(x =>
            x.IsAutoCorrectable && x.ParticipationNumber.HasValue && _stays.ContainsKey(x.ParticipationNumber.Value)
        );

    [Parameter, EditorRequired]
    public IReadOnlyList<StartValidationIssue> Issues { get; set; } = [];

    [Parameter, EditorRequired]
    public Guid EventId { get; set; }

    protected Guid? StaysIn(int participationNumber)
    {
        return _stays.TryGetValue(participationNumber, out var competition) ? competition : null;
    }

    protected void Choose(int participationNumber, Guid? competition)
    {
        if (competition is { } chosen)
        {
            _stays[participationNumber] = chosen;
        }
        else
        {
            _stays.Remove(participationNumber);
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
            foreach (var issue in Issues)
            {
                var number = issue.ParticipationNumber!.Value;
                foreach (var competition in issue.Competitions.Where(x => x.CompetitionId != _stays[number]))
                {
                    await Setup.RemoveParticipation(EventId, number, competition.CompetitionId);
                }
            }

            Issues = await Events.Validate(EventId);
            _stays.Clear();
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
