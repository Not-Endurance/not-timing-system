﻿using NTS.Domain.Setup.Import;
using Newtonsoft.Json;
using NTS.Domain.Extensions;

namespace NTS.Domain.Setup.Entities;

public class EnduranceEvent : DomainEntity, ISummarizable, IImportable, IParent<Official>, IParent<Competition> 
{
    public static EnduranceEvent Create(string? place, Country? country) => new(place, country);

    public static EnduranceEvent Update(
        int id,
        string? place,
        Country? country,
        IEnumerable<Competition> competitions,
        IEnumerable<Official> officials)
        => new(id, place, country, competitions, officials);

    private List<Competition> _competitions = [];
    private List<Official> _officials = [];

    [JsonConstructor]
    private EnduranceEvent(int id, string? place, Country? country, IEnumerable<Competition> competitions, IEnumerable<Official> officials)
        : base(id)
    {
        Place = Capitalized(nameof(Place), place);
        Country = Required(nameof(Country), country);
        _competitions = competitions.ToList();
        _officials = officials.ToList();
    }

    private EnduranceEvent(string? place, Country? country) : this(GenerateId(), place,  country, [], [])
    {
    }

    static string Capitalized(string name, string? value)
    {
        Required(name, value);

        if (string.IsNullOrEmpty(value) || !char.IsUpper(value.First()))
        {
            throw new DomainException(name, $"Has to be Capital case");
        }
        return value;
    }

    public string Place { get; }
    public Country Country { get; }
    public IReadOnlyList<Official> Officials => _officials.AsReadOnly();
    public IReadOnlyList<Competition> Competitions => _competitions.AsReadOnly();

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

    public string Summarize()
	{
		var summary = new Summarizer(this);
        summary.Add(nameof(this.Officials).Localize(), this.Officials);
        summary.Add(nameof(this.Competitions).Localize(), this.Competitions);
        return summary.ToString();
	}

	public override string ToString()
	{
        return Combine(Place, Country);
	}

    private void ValidateRole(Official member)
    {
        var role = member.Role;
        if (member.IsUniqueRole())
        {
            var existing = _officials.FirstOrDefault(x => x.Role == role);
            if (existing != null && existing != member)
            {
                throw new DomainException(
                    nameof(Official.Role),
                    //TODO: Notification should localize the templates and arguments separately
                    string.Format("Official '{0}' already exists", member.Role.GetDescription()));
            }
        }
    }
}
