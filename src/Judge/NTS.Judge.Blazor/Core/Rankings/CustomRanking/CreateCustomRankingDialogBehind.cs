using Not.Application.CRUD.Ports;
using Not.Blazor.Components;
using Not.Exceptions;
using Not.Safe;
using Not.Structures;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Blazor.Core.Rankings.CustomRanking;

public class CreateCustomRankingDialogBehind : NDialog
{
    Ranking? _templateRanking;

    [Inject]
    IRead<Ranking> Rankings { get; set; } = default!;
    [Inject]
    IRead<Participation> Participations { get; set; } = default!;
    [Inject]
    ICustomRankingService Service { get; set; } = default!;
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

    public async Task<IEnumerable<Ranking>> ListRankings()
    {
        return await SafeHelper.Run(Rankings.ReadAll);
    }

    public async Task<IEnumerable<Participation?>> SearchParticipations(string term)
    {
        // TODO: convert to IRepository.Search
        return await SafeHelper.Run(() => Participations.ReadAll(x => x.ToString().Contains(term)));
    }

    public async Task Create()
    {
        await SafeHelper.Run(() => Service.Create(RankingModel));
        Confirm();
    }

    public Task AddEntry()
    {
        SafeHelper.Run(() =>
        {
            var entry = new RankingEntry(EntryToAdd.Participation, EntryToAdd.IsNotRanked);
            RankingModel.Entries.Add(entry);
        });
        return Task.CompletedTask;
    }

    public Task RemoveEntry(RankingEntry entry)
    {
        RankingModel.Entries.Remove(entry);
        return Task.CompletedTask;
    }
}
