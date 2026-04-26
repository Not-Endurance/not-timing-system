using Not.Blazor.Dialogs.Abstractions;
using Not.Structures;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Blazor.Features.Core.Rankings.CustomRanking;

public class CustomRankingDialogBehind : NDialog<CustomRankingModel>
{
    Ranking? _templateRanking;

    [Inject]
    IParticipationContext ParticipationsContext { get; set; } = default!;

    [Inject]
    IRankingContext RankingContext { get; set; } = default!;

    protected IEnumerable<NotListModel<Ranking>> TemplateRankings { get; set; } = [];

    public Ranking? Template
    {
        get => _templateRanking;
        set
        {
            _templateRanking = value;
            if (value != null)
            {
                CustomModel = new CustomRankingModel(value);
            }
        }
    }

    public CustomRankingModel CustomModel { get; set; } = new();
    public CustomRankingEntryModel EntryToAdd { get; set; } = new();

    protected override async Task OnInitializedAsync()
    {
        await Observe(ParticipationsContext);
        await Observe(RankingContext);
    }

    protected override void OnParametersSet()
    {
        try
        {
            TemplateRankings = NotListModel.FromEntity(RankingContext.Rankings);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected Task<IEnumerable<RankingEntry>> GetRankingEntries()
    {
        return Task.FromResult(CustomModel.Entries.AsEnumerable());
    }

    protected Task AddParticipants(Ranking ranking)
    {
        try
        {
            if (ranking == null || CustomModel == null)
            {
                return Task.CompletedTask;
            }
            foreach (var entry in ranking.Entries)
            {
                CustomModel.Entries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }

        return Task.CompletedTask;
    }

    protected Task RemoveParticipationSafe(RankingEntry entry)
    {
        CustomModel.Entries.Remove(entry);
        return Task.CompletedTask;
    }

    protected Task AddParticipation()
    {
        try
        {
            var entry = new RankingEntry(EntryToAdd.Participation, null, EntryToAdd.IsNotRanked);
            CustomModel.Entries.Add(entry);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
        return Task.CompletedTask;
    }

    protected Task<IEnumerable<Participation?>> SearchParticipationsSafe(string term, CancellationToken _)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return Task.FromResult< IEnumerable<Participation?>>([]);
        }
        var matches = ParticipationsContext.Participations
            .Where(x => x.ToString().Contains(term))
            .Cast<Participation?>();
        return Task.FromResult(matches);
    }
}
