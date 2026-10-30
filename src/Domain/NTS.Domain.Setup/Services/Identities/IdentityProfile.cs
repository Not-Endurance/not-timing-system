namespace NTS.Domain.Setup.Services.Identities;

/// <summary>How close two names are, closest first: the order is the order of precedence.</summary>
internal enum NameRelation
{
    Exact = 0,
    Strict = 1,
    Looser = 2,
    NearMiss = 3,
    None = 4,
}

/// <summary>One name of a copy by each of its keys, made once.</summary>
internal sealed class IdentityName
{
    public IdentityName(string name)
    {
        Normalised = IdentityNames.Normalised(name);
        Strict = IdentityNames.Strict(name);
        Looser = IdentityNames.Looser(name);
        Sorted = IdentityNames.Sorted(name);
    }

    public string Normalised { get; }
    public string Strict { get; }
    public string Looser { get; }
    public string Sorted { get; }
}

/// <summary>A copy by the keys it is compared with, made once so that a group of copies is not normalised again for each pair.</summary>
internal sealed class IdentityProfile
{
    public static IdentityProfile Of(IdentityKind kind, IdentityObservation observation)
    {
        var names = new List<IdentityName>();
        foreach (var name in new[] { observation.Name, observation.NameEnglish })
        {
            if (
                IdentityNames.Normalised(name).Length > 0
                && names.All(x => x.Normalised != IdentityNames.Normalised(name))
            )
            {
                names.Add(new IdentityName(name!));
            }
        }

        return new IdentityProfile(
            observation,
            IdentityFeiIds.Of(kind, observation.FeiId),
            IdentityLegacyIds.Of(observation.LegacyId),
            names
        );
    }

    IdentityProfile(IdentityObservation observation, string? feiId, string? legacyId, IReadOnlyList<IdentityName> names)
    {
        Observation = observation;
        FeiId = feiId;
        LegacyId = legacyId;
        Names = names;
    }

    public IdentityObservation Observation { get; }

    /// <summary>The FEI ID to compare by, or none when there is none worth comparing.</summary>
    public string? FeiId { get; }

    /// <summary>The names the copy is known by, each once.</summary>
    public IReadOnlyList<IdentityName> Names { get; }

    /// <summary>The legacy id to compare by, or none when there is none worth comparing.</summary>
    public string? LegacyId { get; }
}
