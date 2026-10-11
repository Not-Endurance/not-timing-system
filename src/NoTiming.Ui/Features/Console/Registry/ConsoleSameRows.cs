using Not.Application.CRUD.Ports;
using Not.Injection;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Ui.Features.Console.Registry;

/// <summary>
/// The registry's guard against a second copy (#646, ADR-0010). The Tenant's own rows are all there to be read and are matched
/// by name and by FEI ID; the other Tenants' are what the cross-tenant search finds for the name that was typed, which the Api
/// limits per account, so a name is searched once and the answer is kept. It decides nothing: the form shows what it finds.
/// </summary>
public class ConsoleSameRows : IConsoleSameRows, IScoped
{
    const int SEARCH_MIN_CHARACTERS = 3;

    readonly IRepository<Athlete> _athletes;
    readonly IRepository<Horse> _horses;
    readonly IConsoleLookups _lookups;
    readonly Dictionary<string, IReadOnlyList<SameRow>> _searched = [];

    public ConsoleSameRows(IRepository<Athlete> athletes, IRepository<Horse> horses, IConsoleLookups lookups)
    {
        _athletes = athletes;
        _horses = horses;
        _lookups = lookups;
    }

    public async Task<IReadOnlyList<SameRow>> OfAthlete(AthleteFormModel model)
    {
        var own = (await _athletes.ReadMany()).Select(x => Row(x.Id, x.Name, x.NameEnglish, x.FeiId, isOwn: true));
        var elsewhere = await Elsewhere(
            "athletes",
            model.Name,
            model.NameEnglish,
            async term =>
                (await _lookups.SearchAthletes(term)).Select(x => Row(x.Id, x.Name, x.NameEnglish, x.FeiId, false))
        );
        return SameRows.Among(Merged(own, elsewhere), model.Id, model.Name, model.NameEnglish, model.FeiId, true);
    }

    public async Task<IReadOnlyList<SameRow>> OfHorse(HorseFormModel model)
    {
        var own = (await _horses.ReadMany()).Select(x => Row(x.Id, x.Name, x.NameEnglish, x.FeiId, isOwn: true));
        var elsewhere = await Elsewhere(
            "horses",
            model.Name,
            model.NameEnglish,
            async term =>
                (await _lookups.SearchHorses(term)).Select(x => Row(x.Id, x.Name, x.NameEnglish, x.FeiId, false))
        );
        return SameRows.Among(Merged(own, elsewhere), model.Id, model.Name, model.NameEnglish, model.FeiId, false);
    }

    static SameRow Row(Guid id, string name, string? nameEnglish, string? feiId, bool isOwn)
    {
        return new SameRow(id, name, nameEnglish, feiId, isOwn, SameRowReason.Name);
    }

    /// <summary>The Tenant's own rows, and of the others' only those that are not among them.</summary>
    static IEnumerable<SameRow> Merged(IEnumerable<SameRow> own, IEnumerable<SameRow> elsewhere)
    {
        var rows = own.ToList();
        return [.. rows, .. elsewhere.Where(x => rows.All(y => y.Id != x.Id))];
    }

    /// <summary>What the other Tenants' registries hold under the name or the name in English that was typed, a name searched once.</summary>
    async Task<IReadOnlyList<SameRow>> Elsewhere(
        string registry,
        string? name,
        string? nameEnglish,
        Func<string, Task<IEnumerable<SameRow>>> search
    )
    {
        var found = new List<SameRow>();
        foreach (var term in new[] { name, nameEnglish }.Select(x => x?.Trim()).Distinct())
        {
            if (term is not { Length: >= SEARCH_MIN_CHARACTERS })
            {
                continue;
            }

            var key = $"{registry}|{term.ToUpperInvariant()}";
            if (!_searched.TryGetValue(key, out var rows))
            {
                rows = _searched[key] = [.. (await search(term)).Where(x => !x.IsOwn)];
            }

            found.AddRange(rows);
        }

        return found;
    }
}
