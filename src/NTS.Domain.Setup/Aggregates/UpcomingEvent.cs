using Newtonsoft.Json;
using Not.Domain.Base;
using Not.Domain.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Extensions;

namespace NTS.Domain.Setup.Aggregates;

public class UpcomingEvent : AggregateRoot, IParent<Official>, IParent<Competition>, IParent<Loop>, IParent<Combination>
{
    public static UpcomingEvent Create(string? place, Country? country, string? showFeiId)
    {
        return new(place, country, showFeiId);
    }

    public static UpcomingEvent Update(
        int? id,
        string? place,
        Country? country,
        string? showFeiId,
        IEnumerable<Competition> competitions,
        IEnumerable<Official> officials,
        IEnumerable<Loop> loops,
        IEnumerable<Combination> combinations)
    {
        return new(id, place, country, showFeiId, competitions, officials, loops, combinations);
    }

    readonly List<Competition> _competitions = [];
    readonly List<Official> _officials = [];
    readonly List<Loop> _loops = [];
    readonly List<Combination> _combinations = [];

    [JsonConstructor]
    UpcomingEvent(
        int? id,
        string? place,
        Country? country,
        string? showFeiId,
        IEnumerable<Competition> competitions,
        IEnumerable<Official> officials,
        IEnumerable<Loop> loops,
        IEnumerable<Combination> combinations)
        : base(id!.Value)
    {
        Place = Capitalized(nameof(Place), place);
        Country = Required(nameof(Country), country);
        ShowFeiId = showFeiId;
        _competitions = competitions.ToList();
        _officials = officials.ToList();
        _loops = loops.ToList();
        _combinations = combinations.ToList();
    }

    UpcomingEvent(string? place, Country? country, string? showFeiId)
        : this(GenerateId(), place, country, showFeiId, [], [], [], []) { }

    public string Place { get; }
    public Country Country { get; }
    public string? ShowFeiId { get; }
    public IReadOnlyList<Competition> Competitions => _competitions.AsReadOnly();
    public IReadOnlyList<Official> Officials => _officials.AsReadOnly();
    public IReadOnlyList<Loop> Loops => _loops.AsReadOnly();
    public IReadOnlyList<Combination> Combinations => _combinations.AsReadOnly();

    public void Add(Competition competition)
    {
        _competitions.Add(competition);
    }

    public void Update(Competition competition)
    {
        _competitions.Update(competition);
    }

    public void Remove(Competition competition)
    {
        _competitions.Remove(competition);
    }

    public void Add(Official official)
    {
        ValidateRole(official);

        _officials.Add(official);
    }

    public void Update(Official official)
    {
        // TODO: fix check for roles
        _officials.Update(official);
    }

    public void Remove(Official official)
    {
        _officials.Remove(official);
    }

    public override string ToString()
    {
        return Combine(Place, Country);
    }

    public void Add(Loop child)
    {
        _loops.Add(child);
    }

    public void Remove(Loop child)
    {
        _loops.Remove(child);
    }

    public void Update(Loop child)
    {
        _loops.Update(child);
    }

    public void Add(Combination child)
    {
        _combinations.Add(child);
    }

    public void Remove(Combination child)
    {
        _combinations.Remove(child);
    }

    public void Update(Combination child)
    {
        _combinations.Update(child);
    }

    static string Capitalized(string name, string? value)
    {
        Required(name, value);

        var character = value.First();
        return value;
    }

    void ValidateRole(Official member)
    {
        var role = member.Role;
        if (!member.IsUniqueRole())
        {
            return;
        }
        var existing = _officials.FirstOrDefault(x => x.Role == role);
        if (existing == null || existing == member)
        {
            return;
        }
        //TODO: Enum localization
        var roleString = member.Role.GetDescription();
        throw new DomainPropertyException(nameof(Official.Role), Official__already_exists, roleString);
    }
}
