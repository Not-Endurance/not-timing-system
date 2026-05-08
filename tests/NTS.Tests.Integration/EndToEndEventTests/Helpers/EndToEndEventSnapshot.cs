using Newtonsoft.Json.Linq;
using NTS.Tests.Integration.Infrastructure;
using CoreParticipationModel = NTS.Application.Contracts.Core.Models.ParticipationModel;
using CoreParticipation = NTS.Domain.Core.Aggregates.Participation;
using CorePhase = NTS.Domain.Core.Aggregates.Participations.Entities.Phase;
using CoreEliminated = NTS.Domain.Core.Aggregates.Participations.Objects.Eliminated;
using CoreRankingModel = NTS.Application.Contracts.Core.Models.RankingModel;
using CoreRanking = NTS.Domain.Core.Aggregates.Ranking;
using SetupAthlete = NTS.Domain.Setup.Aggregates.Athlete;
using SetupClub = NTS.Domain.Setup.Aggregates.Club;
using SetupHorse = NTS.Domain.Setup.Aggregates.Horse;
using SetupConfigureEvent = NTS.Domain.Setup.Aggregates.ConfigureEvent;
using SetupConfigureEventModel = NTS.Application.Contracts.Setup.Models.ConfigureEventModel;
using SetupUser = NTS.Domain.Setup.Aggregates.User;

namespace NTS.Tests.Integration.EndToEndEventTests.Helpers;

internal sealed class EndToEndEventSnapshot
{
    const string CONFIGURE_EVENT_FILE = "configureEvent.json";
    const string PARTICIPATIONS_FILE = "nts.event_participations.json";
    const string RANKINGS_FILE = "nts.event_rankings.json";

    public static IReadOnlyList<string> DiscoverNames()
    {
        var root = SnapshotsRoot();
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"End-to-end snapshot directory was not found: {root}");
        }

        return Directory
            .EnumerateDirectories(root)
            .Where(IsSnapshotDirectory)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToArray();
    }

    public static EndToEndEventSnapshot Load(string name)
    {
        if (Path.GetFileName(name) != name)
        {
            throw new ArgumentException($"Snapshot name must be a directory name, got '{name}'.", nameof(name));
        }

        return LoadDirectory(Path.Combine(SnapshotsRoot(), name));
    }

    readonly JToken _setupSource;
    readonly JToken _participationsSource;
    readonly JToken _rankingsSource;
    readonly IReadOnlyDictionary<int, CoreParticipation> _participationsByNumber;

    EndToEndEventSnapshot(
        string name,
        SetupConfigureEvent configureEvent,
        IReadOnlyList<CoreParticipation> participations,
        IReadOnlyList<CoreRanking> rankings,
        JToken setupSource,
        JToken participationsSource,
        JToken rankingsSource
    )
    {
        Name = name;
        ConfigureEvent = configureEvent;
        Participations = participations;
        Rankings = rankings;
        PhasesWithSnapshots = participations
            .SelectMany(participation =>
                participation.Phases.Select((phase, index) => new EndToEndPhaseSnapshot(participation, phase, index))
            )
            .Where(x => x.ArriveTime != null)
            .OrderBy(x => x.ArriveTime)
            .ThenBy(x => x.Number)
            .ThenBy(x => x.PhaseIndex)
            .ToArray();
        _setupSource = setupSource;
        _participationsSource = SortParticipations(participationsSource);
        _rankingsSource = SortRankings(rankingsSource);
        _participationsByNumber = Participations.ToDictionary(x => x.Combination.Number);
    }

    public string Name { get; }
    public SetupConfigureEvent ConfigureEvent { get; }
    public IReadOnlyList<CoreParticipation> Participations { get; }
    public IReadOnlyList<CoreRanking> Rankings { get; }
    public IReadOnlyList<EndToEndPhaseSnapshot> PhasesWithSnapshots { get; }
    public int EventId => Participations.First().EventId;

    public IReadOnlyList<SetupClub> Clubs =>
        ConfigureEvent
            .Combinations.Select(x => x.Athlete.Club)
            .Where(x => x != null)
            .Select(x => x!)
            .DistinctBy(x => x.Id)
            .ToArray();

    public IReadOnlyList<SetupHorse> Horses =>
        ConfigureEvent.Combinations.Select(x => x.Horse).DistinctBy(x => x.Id).ToArray();

    public IReadOnlyList<SetupAthlete> Athletes =>
        ConfigureEvent.Combinations.Select(x => x.Athlete).DistinctBy(x => x.Id).ToArray();

    public IReadOnlyList<SetupUser> Users =>
        ConfigureEvent
            .Officials.Select(x => x.User)
            .Concat(ConfigureEvent.Combinations.Select(x => x.Athlete.User))
            .Where(x => x != null)
            .Select(x => x!)
            .DistinctBy(x => x.Id)
            .ToArray();

    public CorePhase RequiredPhase(int number, int index)
    {
        return _participationsByNumber[number].Phases[index];
    }

    public IReadOnlyDictionary<int, int> CreateIdMap(
        IReadOnlyList<CoreParticipation> actualParticipations,
        IReadOnlyList<CoreRanking> actualRankings
    )
    {
        var ids = new Dictionary<int, int>();
        foreach (var expected in Participations)
        {
            var actual = actualParticipations.Single(x => x.Combination.Number == expected.Combination.Number);
            ids[expected.Id] = actual.Id;
            for (var i = 0; i < expected.Phases.Count; i++)
            {
                ids[expected.Phases[i].Id] = actual.Phases[i].Id;
            }
        }

        foreach (var expected in Rankings)
        {
            var actual = actualRankings.Single(x =>
                x.Name == expected.Name && x.Category == expected.Category && x.Type == expected.Type
            );
            ids[expected.Id] = actual.Id;
            foreach (var expectedEntry in expected.Entries)
            {
                var actualEntry = actual.Entries.Single(x =>
                    x.Participation.Combination.Number == expectedEntry.Participation.Combination.Number
                );
                ids[expectedEntry.Id] = actualEntry.Id;
            }
        }

        return ids;
    }

    public JToken ExpectedConfigureEventWith(IReadOnlyDictionary<int, int> idMap)
    {
        var expected = _setupSource.DeepClone();
        SnapshotJson.ReplaceIds(expected, idMap);
        return SnapshotJson.Canonicalize(expected);
    }

    public JToken ExpectedParticipationsWith(IReadOnlyDictionary<int, int> idMap)
    {
        var expected = _participationsSource.DeepClone();
        SnapshotJson.ReplaceIds(expected, idMap);
        return SnapshotJson.Canonicalize(expected);
    }

    public JToken ExpectedRankingsWith(IReadOnlyDictionary<int, int> idMap)
    {
        var expected = _rankingsSource.DeepClone();
        SnapshotJson.ReplaceIds(expected, idMap);
        return SnapshotJson.Canonicalize(expected);
    }

    public override string ToString()
    {
        return Name;
    }

    static EndToEndEventSnapshot LoadDirectory(string directory)
    {
        var setupSource = LoadJson(directory, CONFIGURE_EVENT_FILE);
        var setupModel = setupSource.ToObject<SetupConfigureEventModel>(SnapshotJson.Serializer)
            ?? throw new InvalidOperationException($"Could not deserialize setup snapshot '{directory}'.");

        var participationsSource = LoadJson(directory, PARTICIPATIONS_FILE);
        var rankingsSource = LoadJson(directory, RANKINGS_FILE);
        var participationModels = participationsSource.ToObject<CoreParticipationModel[]>(SnapshotJson.Serializer)
            ?? throw new InvalidOperationException($"Could not deserialize participation snapshot '{directory}'.");
        var rankingModels = rankingsSource.ToObject<CoreRankingModel[]>(SnapshotJson.Serializer)
            ?? throw new InvalidOperationException($"Could not deserialize ranking snapshot '{directory}'.");

        return new EndToEndEventSnapshot(
            Path.GetFileName(directory),
            setupModel.MapToEntity(),
            participationModels.Select(x => x.MapToEntity()).ToArray(),
            rankingModels.Select(x => x.MapToEntity()).ToArray(),
            setupSource,
            participationsSource,
            rankingsSource
        );
    }

    static string SnapshotsRoot()
    {
        return Path.Combine(
            RepositoryPaths.Discover().Root,
            "tests",
            "NTS.Tests.Integration",
            "EndToEndEventTests",
            "Snapshots"
        );
    }

    static bool IsSnapshotDirectory(string directory)
    {
        return File.Exists(Path.Combine(directory, CONFIGURE_EVENT_FILE))
            && File.Exists(Path.Combine(directory, PARTICIPATIONS_FILE))
            && File.Exists(Path.Combine(directory, RANKINGS_FILE));
    }

    static JToken LoadJson(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        return SnapshotJson.NormalizeMongoDocument(SnapshotJson.Parse(File.ReadAllText(path)));
    }

    static JToken SortParticipations(JToken token)
    {
        return token is JArray array
            ? new JArray(array.OrderBy(x => x["Combination"]?["Number"]?.Value<int>() ?? 0).Select(x => x.DeepClone()))
            : token.DeepClone();
    }

    static JToken SortRankings(JToken token)
    {
        return token is JArray array
            ? new JArray(
                array
                    .OrderBy(x => x["Name"]?.Value<string>(), StringComparer.Ordinal)
                    .ThenBy(x => x["Category"]?.Value<string>(), StringComparer.Ordinal)
                    .Select(x => x.DeepClone())
            )
            : token.DeepClone();
    }
}

internal sealed class EndToEndPhaseSnapshot
{
    public EndToEndPhaseSnapshot(CoreParticipation participation, CorePhase phase, int phaseIndex)
    {
        Participation = participation;
        Phase = phase;
        PhaseIndex = phaseIndex;
    }

    public CoreParticipation Participation { get; }
    public CorePhase Phase { get; }
    public int PhaseIndex { get; }
    public int PhaseNumber => PhaseIndex + 1;
    public int Number => Participation.Combination.Number;
    public DateTimeOffset? ArriveTime => Phase.ArriveTime?.ToDateTimeOffset();
    public DateTimeOffset? PresentTime => Phase.PresentTime?.ToDateTimeOffset();
    public DateTimeOffset? RepresentTime => Phase.RepresentTime?.ToDateTimeOffset();
    public CoreEliminated? ExpectedEliminated => Participation.Eliminated;
}
