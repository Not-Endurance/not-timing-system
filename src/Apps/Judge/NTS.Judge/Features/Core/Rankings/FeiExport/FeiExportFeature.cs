using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Not.Domain.Exceptions;
using Not.Formatting;
using Not.Injection;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Enums;

namespace NTS.Judge.Features.Core.Rankings.FeiExport;

internal class FeiExportFeature : IFeiExportFeature
{
    const int FEI_RANKING_CONFIGURATION_FIELD_COUNT = 5;

    public string CreateXmlContent(
        EventInformation eventInformation,
        IEnumerable<Result> results,
        IEnumerable<Ranking> rankings
    )
    {
        var model = CreateHorseSport(eventInformation, results.ToList(), rankings.ToList());
        var xml = Serialize(model);
        return InsertGeneratedDate(xml);
    }

    HorseSport CreateHorseSport(
        EventInformation eventInformation,
        IReadOnlyCollection<Result> results,
        IReadOnlyCollection<Ranking> rankings
    )
    {
        if (string.IsNullOrWhiteSpace(eventInformation.FeiShowId))
        {
            throw new DomainException("Missing Show FEI ID");
        }
        if (string.IsNullOrWhiteSpace(eventInformation.Location))
        {
            throw new DomainException("Missing event information location");
        }

        var configuredResults = GetConfiguredResults(results, rankings).ToList();
        if (!configuredResults.Any())
        {
            throw new DomainException("No rankings are configured for FEI export");
        }

        // IsoCode is not accepted by FEI, but they have representatives which can correct that in case a country
        // without NF code is used. This shouldn't happen anyway
        var countryCode = eventInformation.Country.NfCode ?? eventInformation.Country.IsoCode;
        var enduranceEvents = configuredResults
            .OrderBy(x => x.Ranking.FeiEventId)
            .ThenBy(x => x.Ranking.FeiEventCode)
            .GroupBy(x => new { x.Ranking.FeiEventId, x.Ranking.FeiEventCode })
            .Select(group =>
                CreateEnduranceEvent(
                    eventInformation,
                    countryCode,
                    group.Key.FeiEventId!,
                    group.Key.FeiEventCode!,
                    group
                )
            )
            .ToArray();

        var horseSport = new HorseSport()
        {
            Generated = new ctGenerated
            {
                Software = "NTS",
                SoftwareVersion = "1.0.0",
                Organization = "NotACompany",
            },
            EventResult = new ctShowResultType
            {
                Show = new ctShowResult
                {
                    Venue = new ctVenue { Name = eventInformation.Location, Country = countryCode },
                    EnduranceEvent = enduranceEvents,
                    StartDate = eventInformation.EventSpan.StartDay.DateTime,
                    EndDate = eventInformation.EventSpan.EndDay.DateTime,
                    FEIID = eventInformation.FeiShowId,
                },
            },
        };
        return horseSport;
    }

    IEnumerable<ConfiguredResults> GetConfiguredResults(
        IEnumerable<Result> results,
        IReadOnlyCollection<Ranking> rankings
    )
    {
        var rankingsById = rankings.ToDictionary(x => x.Id);
        foreach (var result in results)
        {
            var ranking = GetMatchingRanking(result, rankingsById);
            var missing = GetMissingFeiConfiguration(ranking).ToList();
            if (missing.Count == FEI_RANKING_CONFIGURATION_FIELD_COUNT)
            {
                continue;
            }
            if (missing.Any())
            {
                throw new DomainException(
                    $"Ranking '{ranking.Name}' is missing FEI export configuration: {string.Join(", ", missing)}"
                );
            }
            if (string.IsNullOrWhiteSpace(result.Name))
            {
                throw new DomainException("Missing ranking Name");
            }

            yield return new ConfiguredResults(result, ranking);
        }
    }

    static Ranking GetMatchingRanking(Result results, IReadOnlyDictionary<int, Ranking> rankingsById)
    {
        if (results.RankingId != null && rankingsById.TryGetValue(results.RankingId.Value, out var ranking))
        {
            return ranking;
        }

        throw new DomainException($"Results '{results.Name}' cannot be matched to a ranking for FEI export");
    }

    static IEnumerable<string> GetMissingFeiConfiguration(Ranking ranking)
    {
        if (string.IsNullOrWhiteSpace(ranking.FeiEventId))
        {
            yield return "FEI Event ID";
        }
        if (string.IsNullOrWhiteSpace(ranking.FeiEventCode))
        {
            yield return "FEI Event Code";
        }
        if (string.IsNullOrWhiteSpace(ranking.FeiCompetitionId))
        {
            yield return "FEI Competition ID";
        }
        if (string.IsNullOrWhiteSpace(ranking.FeiRule))
        {
            yield return "FEI Rule";
        }
        if (string.IsNullOrWhiteSpace(ranking.FeiScheduleNumber))
        {
            yield return "FEI Schedule NR";
        }
    }

    ctEnduranceEvent CreateEnduranceEvent(
        EventInformation eventInformation,
        string countryCode,
        string feiEventId,
        string feiEventCode,
        IEnumerable<ConfiguredResults> results
    )
    {
        return new ctEnduranceEvent
        {
            FEIID = feiEventId,
            Code = feiEventCode,
            StartDate = eventInformation.EventSpan.StartDay.DateTime,
            EndDate = eventInformation.EventSpan.EndDay.DateTime,
            NF = countryCode,
            Competitions = results.Select(x => CreateCompetition(eventInformation, x)).ToArray(),
        };
    }

    ctEnduranceCompetition CreateCompetition(EventInformation eventInformation, ConfiguredResults configuredResults)
    {
        var results = configuredResults.Results;
        var ranking = configuredResults.Ranking;
        var ctCompetition = new ctEnduranceCompetition
        {
            FEIID = ranking.FeiCompetitionId!,
            ScheduleCompetitionNr = ranking.FeiScheduleNumber!,
            Rule = ranking.FeiRule!,
            Name = results.Name,
            StartDate = eventInformation.EventSpan.StartDay.DateTime,
            Team = false,
            ParticipationList = new ctEnduranceParticipations(),
        };

        var ctParticipations = CreateParticipations(results);
        ctCompetition.ParticipationList.Participation = ctParticipations.ToArray();
        return ctCompetition;
    }

    IEnumerable<ctEnduranceIndivResult> CreateParticipations(Result results)
    {
        var entries = results.Entries;
        var withoutFeiId = entries.Where(x =>
            string.IsNullOrWhiteSpace(x.Participation.Combination.Athlete.FeiId)
            || string.IsNullOrWhiteSpace(x.Participation.Combination.Horse.FeiId)
        );
        if (withoutFeiId.Any())
        {
            var numbers = withoutFeiId.Select(x => x.Participation.Combination.Number);
            var formatted = string.Join(", ", numbers);
            throw new DomainException($"Participants '{formatted}' are not configured with Athlete/Horse FEI ID");
        }
        foreach (var entry in entries)
        {
            var athlete = entry.Participation.Combination.Athlete;
            var horse = entry.Participation.Combination.Horse;
            var (First, Last) = SplitName(athlete.GetDisplayName(CompetitionRuleset.FEI));

            var ctParticipation = new ctEnduranceIndivResult
            {
                Athlete = new ctEnduranceAthlete
                {
                    FEIID = int.Parse(athlete.FeiId!),
                    AthleteNumber = entry.Participation.Combination.Number,
                    FirstName = First,
                    FamilyName = Last,
                    CompetingFor = athlete.Country.NfCode ?? athlete.Country.IsoCode,
                },
                Horse = new ctHorse { FEIID = horse.FeiId!, Name = horse.GetDisplayName(CompetitionRuleset.FEI) },
                Complement = new ctEnduranceComplement { BestCondition = false },
                Position = new ctPositionIndiv { Status = entry.Participation.Eliminated?.Code ?? "R" },
            };
            if (ctParticipation.Position.Status == "R" && entry.Rank != null)
            {
                ctParticipation.Position.Rank = entry.Rank.Value;
            }

            var ctDays = CreateDaysAndPhases(entry.Participation);
            ctParticipation.Phases = ctDays.ToArray();

            if (entry.Participation.Eliminated == null)
            {
                ctParticipation.Total = CreateTotal(entry.Participation);
            }

            yield return ctParticipation;
        }
    }

    static (string First, string Last) SplitName(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return (name, name);
        }

        return (parts.First(), parts.Last());
    }

    ctEnduranceTotal CreateTotal(Participation participation)
    {
        var total = participation.GetTotal();
        var time = total?.Interval.ToTimeSpan() ?? TimeSpan.Zero;
        var speed = total?.AverageSpeed.ToDouble() ?? default;
        return new ctEnduranceTotal { AverageSpeed = Round(speed), Time = FormattingHelper.Format(time) };
    }

    IEnumerable<ctEnduranceDayResult> CreateDaysAndPhases(Participation participation)
    {
        var days = new List<ctEnduranceDayResult>();
        var day = new ctEnduranceDayResult() { Number = 1 };
        var ctPhases = new List<ctEndurancePhaseResult>();
        var phases = participation.Phases.Where(x => x.StartTime != null);
        var lastDay = phases.First().StartTime!.ToDateTimeOffset().Day;
        foreach (var phase in phases)
        {
            var averagePhaseSpeed = phase.GetAveragePhaseSpeed()?.ToDouble() ?? 0;
            var phaseInterval = phase.GetPhaseInterval()?.ToTimeSpan() ?? TimeSpan.Zero;
            var recoveryInterval = phase.GetRecoveryInterval()?.ToTimeSpan() ?? TimeSpan.Zero;
            var ctPhase = new ctEndurancePhaseResult
            {
                Number = participation.Phases.IndexOf(phase) + 1,
                Result = new ctEndurancePhaseResultScore
                {
                    PhaseAverageSpeed = Round(averagePhaseSpeed),
                    PhaseTime = FormattingHelper.Format(phaseInterval),
                    RecoveryTime = FormattingHelper.Format(recoveryInterval),
                },
            };
            if (lastDay == phase.StartTime!.ToDateTimeOffset().Day)
            {
                ctPhases.Add(ctPhase);
            }
            else
            {
                day.Phase = ctPhases.ToArray();
                days.Add(day);
                var number = day.Number + 1;
                day = new ctEnduranceDayResult { Number = number };
                ctPhases = [ctPhase];
            }
        }
        day.Phase = ctPhases.ToArray();
        days.Add(day);

        if (participation.Eliminated == null)
        {
            return days;
        }
        var eliminationCode = participation.Eliminated.Code;
        if (participation.Eliminated is FailedToQualify failedToQualify)
        {
            var codes = failedToQualify.FtqCodes.Select(x => x.ToString());
            eliminationCode = string.Join(" ", codes);
        }
        var lastCtPhase = days.Last().Phase.Last();
        lastCtPhase.VetInspection = new ctEnduranceVetInspection
        {
            Type = stEnduranceVetTypeCode.Standard,
            EliminationCode = eliminationCode,
        };

        return days;
    }

    string Serialize(HorseSport horseSport)
    {
        var serializer = new XmlSerializer(typeof(HorseSport));
        using var memoryStream = new MemoryStream();
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
        using var writer = XmlWriter.Create(memoryStream, settings);
        serializer.Serialize(writer, horseSport);
        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }

    string InsertGeneratedDate(string xml)
    {
        var now = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);
        var generated = $"<Generated Date=\"{now}+00:00\" Software";
        return xml.Replace("<Generated Software", generated);
    }

    decimal Round(double value)
    {
        return (decimal)Math.Round(value, 2);
    }
}

public interface IFeiExportFeature : ITransient
{
    string CreateXmlContent(
        EventInformation eventInformation,
        IEnumerable<Result> results,
        IEnumerable<Ranking> rankings
    );
}

sealed class ConfiguredResults
{
    public ConfiguredResults(Result results, Ranking ranking)
    {
        Results = results;
        Ranking = ranking;
    }

    public Result Results { get; }
    public Ranking Ranking { get; }
}
