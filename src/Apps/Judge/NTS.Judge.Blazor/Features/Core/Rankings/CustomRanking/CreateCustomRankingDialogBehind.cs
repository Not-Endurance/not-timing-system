using Not.Application.CRUD.Ports;
using Not.Blazor.Components;
using Not.Safe;
using Not.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Judge.Features.Core.Rankings.CustomRankings;

namespace NTS.Judge.Blazor.Features.Core.Rankings.CustomRanking;

public class CreateCustomRankingDialogBehind : NDialog
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
    public RankingEntryModel EntryToAdd { get; set; } = new();

    protected override async Task OnParametersSetAsync()
    {
        var listRankings = await ListRankings();
        TemplateRankings = NotListModel.FromEntity<Ranking>(listRankings).ToList();
    }

    protected Task<IEnumerable<RankingEntry>> GetRankingEntries()
    {
        return Task.FromResult(RankingModel.Entries.AsEnumerable());
    }

    protected Task Test(CustomRankingModel _)
    {
        return Task.CompletedTask;
    }

    protected Task CombineRankings(Ranking? ranking)
    {
        if (ranking == null)
        {
            return Task.CompletedTask;
        }
        if (RankingModel == null)
        {
            RankingModel = new CustomRankingModel(ranking);
        }
        else
        {
            foreach (var entry in ranking.Entries)
            {
                SafeHelper.Run(() =>
                {
                    RankingModel.Entries.Add(entry);
                });
            }
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
