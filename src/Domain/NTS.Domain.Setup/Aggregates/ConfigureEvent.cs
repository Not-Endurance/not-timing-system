using Not.Domain.Exceptions;
using Not.Domain.Krud;
using NTS.Domain.Aggregates;
using NTS.Domain.Extensions;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Domain.Setup.Aggregates;

public class ConfigureEvent
    : Aggregate,
        IKrudParent<Official>,
        IKrudParent<Competition>,
        IKrudParent<Loop>,
        IKrudParent<Combination>
{
    readonly List<Competition> _competitions = [];
    readonly List<Official> _officials = [];
    readonly List<Loop> _loops = [];
    readonly List<Combination> _combinations = [];

    public ConfigureEvent(
        string? name,
        string? location,
        Country? country,
        string? feiShowId,
        IEnumerable<Competition> competitions,
        IEnumerable<Official> officials,
        IEnumerable<Loop> loops,
        IEnumerable<Combination> combinations,
        int? id = null
    )
        : base(id)
    {
        Name = Required(nameof(Name), name);
        Location = Required(nameof(Location), location);
        Country = Required(nameof(Country), country);
        FeiShowId = feiShowId;
        _competitions = competitions.ToList();
        _officials = officials.ToList();
        _loops = loops.ToList();
        _combinations = combinations.ToList();
        ValidateUniqueSetup();
    }

    IReadOnlyList<Official> IKrudParent<Official>.Children => Officials;
    IReadOnlyList<Competition> IKrudParent<Competition>.Children => Competitions;
    IReadOnlyList<Loop> IKrudParent<Loop>.Children => Loops;
    IReadOnlyList<Combination> IKrudParent<Combination>.Children => Combinations;

    public string Name { get; }
    public string Location { get; }
    public Country Country { get; }
    public string? FeiShowId { get; }
    public IReadOnlyList<Competition> Competitions => _competitions.AsReadOnly();
    public IReadOnlyList<Official> Officials => _officials.AsReadOnly();
    public IReadOnlyList<Loop> Loops => _loops.AsReadOnly();
    public IReadOnlyList<Combination> Combinations => _combinations.AsReadOnly();

    public void Add(Competition competition)
    {
        ValidateUniqueName(competition);
        _competitions.Add(competition);
    }

    public void Update(Competition competition)
    {
        ValidateUniqueName(competition);
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
        return Combine(Name, Location, Country);
    }

    public void Add(Loop child)
    {
        ValidateUniqueDistance(child);
        _loops.Add(child);
    }

    public void Remove(Loop child)
    {
        _loops.Remove(child);
    }

    public void Update(Loop child)
    {
        ValidateUniqueDistance(child);
        _loops.Update(child);
    }

    public void Add(Combination child)
    {
        ValidateUniqueNumber(child);
        ValidateUniqueAthlete(child);
        ValidateUniqueHorse(child);
        _combinations.Add(child);
    }

    public void Remove(Combination child)
    {
        _combinations.Remove(child);
    }

    public void Update(Combination child)
    {
        ValidateUniqueNumber(child);
        ValidateUniqueAthlete(child);
        ValidateUniqueHorse(child);
        _combinations.Update(child);
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

    void ValidateUniqueSetup()
    {
        foreach (var competition in _competitions)
        {
            ValidateUniqueName(competition);
        }

        foreach (var loop in _loops)
        {
            ValidateUniqueDistance(loop);
        }

        foreach (var combination in _combinations)
        {
            ValidateUniqueNumber(combination);
            ValidateUniqueAthlete(combination);
            ValidateUniqueHorse(combination);
        }
    }

    void ValidateUniqueName(Competition competition)
    {
        var name = competition.Name.Trim();
        var exists = _competitions.Any(x =>
            x.Id != competition.Id && string.Equals(x.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)
        );
        if (exists)
        {
            throw new DomainPropertyException(nameof(Competition.Name), Competition__already_exists, competition.Name);
        }
    }

    void ValidateUniqueDistance(Loop loop)
    {
        var exists = _loops.Any(x => x.Id != loop.Id && x.Distance == loop.Distance);
        if (exists)
        {
            throw new DomainPropertyException(nameof(Loop.Distance), Loop_distance__already_exists, loop.Distance);
        }
    }

    void ValidateUniqueNumber(Combination combination)
    {
        var exists = _combinations.Any(x => x.Id != combination.Id && x.Number == combination.Number);
        if (exists)
        {
            throw new DomainPropertyException(
                nameof(Combination.Number),
                Combination_number__already_exists,
                combination.Number
            );
        }
    }

    void ValidateUniqueAthlete(Combination combination)
    {
        var exists = _combinations.Any(x => x.Id != combination.Id && x.Athlete.Id == combination.Athlete.Id);
        if (exists)
        {
            throw new DomainPropertyException(
                nameof(Combination.Athlete),
                Combination_athlete__already_exists,
                combination.Athlete
            );
        }
    }

    void ValidateUniqueHorse(Combination combination)
    {
        var exists = _combinations.Any(x => x.Id != combination.Id && x.Horse.Id == combination.Horse.Id);
        if (exists)
        {
            throw new DomainPropertyException(
                nameof(Combination.Horse),
                Combination_horse__already_exists,
                combination.Horse
            );
        }
    }
}
