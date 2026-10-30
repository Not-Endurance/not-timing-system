using Not.Application.CRUD.Ports;
using Not.Injection;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Services.Registry;

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
    readonly Dictionary<string, IReadOnlyList<RegistryRow>> _searched = [];

    public ConsoleSameRows(IRepository<Athlete> athletes, IRepository<Horse> horses, IConsoleLookups lookups)
    {
        _athletes = athletes;
        _horses = horses;
        _lookups = lookups;
    }

    public async Task<IReadOnlyList<SameRow>> OfAthlete(AthleteFormModel model)
    {
        var own = (await _athletes.ReadMany())
            .Select(x => new RegistryRow(x.Id, x.Name, x.NameEnglish, x.FeiId))
            .ToList();
        var elsewhere = await Elsewhere(
            "athletes",
            model.Name,
            model.NameEnglish,
            async term =>
                (await _lookups.SearchAthletes(term)).Select(x => new RegistryRow(x.Id, x.Name, x.NameEnglish, x.FeiId))
        );
        var matches = SameRows.Among(
            Merged(own, elsewhere),
            model.Id,
            model.Name,
            model.NameEnglish,
            model.FeiId,
            true
        );
        return Shown(matches, own);
    }

    public async Task<IReadOnlyList<SameRow>> OfHorse(HorseFormModel model)
    {
        var own = (await _horses.ReadMany())
            .Select(x => new RegistryRow(x.Id, x.Name, x.NameEnglish, x.FeiId))
            .ToList();
        var elsewhere = await Elsewhere(
            "horses",
            model.Name,
            model.NameEnglish,
            async term =>
                (await _lookups.SearchHorses(term)).Select(x => new RegistryRow(x.Id, x.Name, x.NameEnglish, x.FeiId))
        );
        var matches = SameRows.Among(
            Merged(own, elsewhere),
            model.Id,
            model.Name,
            model.NameEnglish,
            model.FeiId,
            false
        );
        return Shown(matches, own);
    }

    /// <summary>The Tenant's own rows, and of the others' only those that are not among them. The own rows come first.</summary>
    static IEnumerable<RegistryRow> Merged(IEnumerable<RegistryRow> own, IEnumerable<RegistryRow> elsewhere)
    {
        var rows = own.ToList();
        return [.. rows, .. elsewhere.Where(x => rows.All(y => y.Id != x.Id))];
    }

    /// <summary>The matches as the form shows them: whether each is one of the Tenant's own rows, and why it is the same.</summary>
    static IReadOnlyList<SameRow> Shown(IEnumerable<SameMatch> matches, IEnumerable<RegistryRow> own)
    {
        var ownIds = own.Select(x => x.Id).ToHashSet();
        return
        [
            .. matches.Select(x => new SameRow(
                x.Row.Id,
                x.Row.Name,
                x.Row.NameEnglish,
                x.Row.FeiId,
                ownIds.Contains(x.Row.Id),
                x.Reason == SameReason.FeiId ? SameRowReason.FeiId : SameRowReason.Name
            )),
        ];
    }

    /// <summary>What the other Tenants' registries hold under the name or the name in English that was typed, a name searched once.</summary>
    async Task<IReadOnlyList<RegistryRow>> Elsewhere(
        string registry,
        string? name,
        string? nameEnglish,
        Func<string, Task<IEnumerable<RegistryRow>>> search
    )
    {
        var found = new List<RegistryRow>();
        foreach (var term in new[] { name, nameEnglish }.Select(x => x?.Trim()).Distinct())
        {
            if (term is not { Length: >= SEARCH_MIN_CHARACTERS })
            {
                continue;
            }

            var key = $"{registry}|{term.ToUpperInvariant()}";
            if (!_searched.TryGetValue(key, out var rows))
            {
                rows = _searched[key] = [.. await search(term)];
            }

            found.AddRange(rows);
        }

        return found;
    }
}
