using Not.Blazor.Dialogs.Abstractions;
using Not.Notify;
using Not.Structures;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Console.Rankings;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Console.Features.Rankings.CustomRanking;

/// <summary>
/// A Ranking that the Main Operator makes of the Participations it chooses (#646), over the settings of a Ranking it may copy
/// (the name is its own, to be given). The Participations are those of the Event the page shows; the Ranking is made and
/// stored by the form, and the Api's refusal stays in the dialog with what was typed.
/// </summary>
public class CustomRankingDialogBehind : NDialog<CustomRankingModel>
{
    Ranking? _template;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    protected IEnumerable<NotListModel<Ranking>> TemplateRankings => NotListModel.FromEntity(View.Rankings);

    protected Ranking? Template
    {
        get => _template;
        set
        {
            _template = value;
            if (value != null)
            {
                CustomModel = new CustomRankingModel(value) { Entries = [.. CustomModel.Entries] };
            }
        }
    }

    protected CustomRankingModel CustomModel { get; set; } = new();
    protected CustomRankingEntryModel EntryToAdd { get; set; } = new();

    [Parameter, EditorRequired]
    public IViewedEvent View { get; set; } = default!;

    protected Task AddParticipation()
    {
        if (EntryToAdd.Participation == null)
        {
            Notifier.Warn(Select_participation_to_add_string);
            return Task.CompletedTask;
        }

        if (CustomModel.Entries.Any(x => x.Participation!.Id == EntryToAdd.Participation.Id))
        {
            return Task.CompletedTask; // a Ranking counts a Participation once
        }

        CustomModel.Entries.Add(EntryToAdd);
        EntryToAdd = new();
        return Task.CompletedTask;
    }

    protected Task RemoveParticipationSafe(CustomRankingEntryModel entry)
    {
        CustomModel.Entries.Remove(entry);
        return Task.CompletedTask;
    }

    protected Task<IEnumerable<Participation?>> SearchParticipationsSafe(string term, CancellationToken _)
    {
        var matches = View
            .Participations.Where(x =>
                string.IsNullOrWhiteSpace(term)
                || x.ToString().Contains(term, StringComparison.CurrentCultureIgnoreCase)
            )
            .Cast<Participation?>();
        return Task.FromResult(matches);
    }
}
