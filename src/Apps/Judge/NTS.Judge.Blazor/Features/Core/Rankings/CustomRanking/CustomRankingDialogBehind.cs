using Not.Application.CRUD.Ports;
using Not.Blazor.Dialogs.Abstractions;
using Not.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.Features.Core.Rankings.CustomRankings;

namespace NTS.Judge.Blazor.Features.Core.Rankings.CustomRanking;

public class CustomRankingDialogBehind : NDialog<CustomRankingModel>
{
    Ranking? _templateRanking;

    [Inject]
    IReadMany<Ranking> Rankings { get; set; } = default!;

    [Inject]
    IReadMany<Participation> Participations { get; set; } = default!;

    protected List<NotListModel<Ranking>> TemplateRankings { get; set; } = [];

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

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            var listRankings = await Rankings.ReadMany();
            TemplateRankings = NotListModel.FromEntity(listRankings).ToList();
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

    protected async Task<IEnumerable<Participation?>> SearchParticipationsSafe(string term, CancellationToken _)
    {
        // TODO: convert to IRepository.Search
        return await Participations.ReadMany(x => x.ToString().Contains(term));
    }
}
