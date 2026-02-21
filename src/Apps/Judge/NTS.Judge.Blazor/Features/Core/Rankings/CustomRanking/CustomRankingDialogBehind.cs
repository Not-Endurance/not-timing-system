using Not.Application.CRUD.Ports;
using Not.Blazor.Components.Abstractions;
using Not.Safe;
using Not.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.Features.Core.Rankings.CustomRankings;
using Not.Blazor.Dialogs.Abstractions;

namespace NTS.Judge.Blazor.Features.Core.Rankings.CustomRanking;

public class CustomRankingDialogBehind : NDialog<CustomRankingModel>
{
    Ranking? _templateRanking;

    [Inject]
    IReadMany<Ranking> Rankings { get; set; } = default!;

    [Inject]
    IReadMany<Participation> Participations { get; set; } = default!;

    protected List<NotListModel<Ranking>> TemplateRankings { get; set; } = [];

    public Ranking? TemplateRanking
    {
        get => _templateRanking;
        set
        {
            _templateRanking = value;
            CombineRankings(_templateRanking);
        }
    }

    public CustomRankingModel RankingModel { get; set; } = new();
    public CustomRankingEntryModel EntryToAdd { get; set; } = new();

    protected override async Task OnParametersSetAsync()
    {
        var listRankings = await ListRankings();
        TemplateRankings = NotListModel.FromEntity<Ranking>(listRankings).ToList();
    }

    protected Task<IEnumerable<RankingEntry>> GetRankingEntries()
    {
        return Task.FromResult(RankingModel.Entries.AsEnumerable());
    }

    protected Task CombineRankings(Ranking? ranking) // TODO: split this into template and add participants
    {
        try
        {
            if (ranking == null)
            {
                return Task.CompletedTask;
            }
            if (RankingModel.Name == null || RankingModel.Category == null || RankingModel.Ruleset == null || RankingModel.Type == null)
            {
                RankingModel = new CustomRankingModel(ranking);
            }
            else
            {
                foreach (var entry in ranking.Entries)
                {
                    RankingModel.Entries.Add(entry);
                }
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }

        return Task.CompletedTask;
    }

    protected Task DeleteSafe(RankingEntry entry)
    {
        RankingModel.Entries.Remove(entry);
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<Ranking>> ListRankings()
    {
        return await SafeHelper.Run(Rankings.ReadMany);
    }

    public async Task<IEnumerable<Participation?>> SearchParticipations(string term, CancellationToken _)
    {
        // TODO: convert to IRepository.Search
        return await Participations.ReadMany(x => x.ToString().Contains(term));
    }

    public Task AddEntry()
    {
        SafeHelper.Run(() =>
        {
            var entry = new RankingEntry(EntryToAdd.Participation, null, EntryToAdd.IsNotRanked);
            RankingModel.Entries.Add(entry);
        });
        return Task.CompletedTask;
    }
}
