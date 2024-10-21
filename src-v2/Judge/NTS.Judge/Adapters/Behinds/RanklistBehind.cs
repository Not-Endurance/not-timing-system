using Not.Application.Adapters.Behinds;
using Not.Application.Ports.CRUD;
using Not.Exceptions;
using Not.Safe;
using NTS.Domain.Core.Entities;
using NTS.Domain.Core.Objects;
using NTS.Judge.Blazor.Ports;

namespace NTS.Judge.Adapters.Behinds;

public class RanklistBehind : ObservableBehind, IRanklistBehind
{
    private readonly IRepository<Ranking> _rankings;

    public RanklistBehind(IRepository<Ranking> rankings)
    {
        _rankings = rankings;
    }

    public Ranklist? Ranklist { get; private set; }

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        var ranking = await _rankings.Read(x => true);
        if (ranking == null)
        {
            return false;
        }
        Ranklist = new Ranklist(ranking);
        return true;
    }

    async Task<IEnumerable<Ranking>> SafeGetRankings()
    {
        return await _rankings.ReadAll();
    }

    async Task SafeSelectRanking(int id)
    {
        var ranking = await _rankings.Read(id);
        GuardHelper.ThrowIfDefault(ranking);

        Ranklist = new Ranklist(ranking);
        EmitChange();
    }

    #region SafePattern
    
    public async Task<IEnumerable<Ranking>> GetRankings()
    {
        return await SafeHelper.Run(SafeGetRankings) ?? [];
    }

    public async Task SelectRanking(int id)
    {
        await SafeHelper.Run(() => SafeSelectRanking(id));
    }

    #endregion
}
