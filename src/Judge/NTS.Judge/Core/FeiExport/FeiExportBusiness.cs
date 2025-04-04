using System.Globalization;
using System.Xml.Serialization;
using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using Not.Exceptions;
using Not.Injection;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;

namespace NTS.Judge.Core.FeiExport;

public class FeiExportBusiness : IFeiExportBusiness
{
    readonly IRepository<EnduranceEvent> _events;

    public FeiExportBusiness(IRepository<EnduranceEvent> events)
    {
        _events = events;
    }

    public async Task<string> Create(Ranklist ranklist)
    {
        var enduranceEvent = await _events.Read(0);
        var horseSport = CreateHorseSport(enduranceEvent!, ranklist);
        var xml = Serialize(horseSport);
        return InsertGeneratedDate(xml);
    }

    HorseSport CreateHorseSport(EnduranceEvent enduranceEvent, Ranklist ranklist)
    {
        var ranking = ranklist.Ranking;
        if (string.IsNullOrWhiteSpace(enduranceEvent.FeiShowId))
        {
            throw new DomainException("Missing Show FEIID");
        }
        if (string.IsNullOrEmpty(enduranceEvent.PopulatedPlace?.Location))
        {
            throw new DomainException("Missing PopulatedPlace");
        }
        if (string.IsNullOrEmpty(ranking.Name))
        {
            throw new DomainException("Missing ranking Name");
        }
        if (string.IsNullOrEmpty(ranking.FeiCategoryEventNumber))
        {
            throw new DomainException("Missing FEI Category Event NR");
        }
        if (string.IsNullOrEmpty(ranking.FeiScheduleNumber))
        {
            throw new DomainException("Missing FEI Schedule NR");
        }
        if (string.IsNullOrEmpty(ranking.FeiRule))
        {
            throw new DomainException("Missing FEI Rule");
        }
        if (string.IsNullOrEmpty(ranking.FeiEventCode))
        {
            throw new DomainException("Missing FEI Event Code");
        }
        if (string.IsNullOrEmpty(enduranceEvent.PopulatedPlace.Country.NfCode))
        {
            throw new DomainException(
                $"Country '{enduranceEvent.PopulatedPlace.Country}' does not have 'NF' code. Contact developer"
            );
        }

        var feiCategory = GetFeiCategory(ranking.Category);
        var competitionFeiId = $"{enduranceEvent.FeiShowId}_E_{feiCategory}_{ranking.FeiCategoryEventNumber}";
        var ctEnduranceCompetition = CreateCompetitions(enduranceEvent!, ranklist);
        var ctEnduranceEvent = new ctEnduranceEvent
        {
            FEIID = competitionFeiId,
            Code = ranking.FeiEventCode,
            StartDate = enduranceEvent.EventSpan.StartDay.DateTime,
            EndDate = enduranceEvent.EventSpan.EndDay.DateTime,
            NF = enduranceEvent.PopulatedPlace.Country.NfCode,
            Competitions = new ctEnduranceCompetition[] { ctEnduranceCompetition },
        };
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
                    Venue = new ctVenue
                    {
                        Name = enduranceEvent.PopulatedPlace.Location,
                        Country = enduranceEvent.PopulatedPlace.Country.NfCode,
                    },
                    EnduranceEvent = new List<ctEnduranceEvent> { ctEnduranceEvent }.ToArray(),
                    StartDate = enduranceEvent.EventSpan.StartDay.DateTime,
                    EndDate = enduranceEvent.EventSpan.EndDay.DateTime,
                    FEIID = enduranceEvent.FeiShowId,
                },
            },
        };
        return horseSport;
    }

    ctEnduranceCompetition CreateCompetitions(EnduranceEvent enduranceEvent, Ranklist ranklist)
    {
        var ranking = ranklist.Ranking;
        var categoryString = GetFeiCategory(ranking.Category);
        var competitionFeiId =
            $"{enduranceEvent.FeiShowId}_E_{categoryString}_{ranking.FeiCategoryEventNumber}_{ranking.FeiScheduleNumber}";
        var ctCompetition = new ctEnduranceCompetition
        {
            FEIID = competitionFeiId,
            ScheduleCompetitionNr = ranking.FeiScheduleNumber!,
            Rule = ranking.FeiRule!,
            Name = ranking.Name,
            StartDate = enduranceEvent.EventSpan.StartDay.DateTime,
            Team = false,
            ParticipationList = new ctEnduranceParticipations(),
        };

        var ctParticipations = CreateParticipations(ranklist);
        ctCompetition.ParticipationList.Participation = ctParticipations.ToArray();
        return ctCompetition;
    }

    IEnumerable<ctEnduranceIndivResult> CreateParticipations(Ranklist ranklist)
    {
        var entries = ranklist.Entries;
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

            var ctParticipation = new ctEnduranceIndivResult
            {
                Athlete = new ctEnduranceAthlete
                {
                    FEIID = int.Parse(athlete.FeiId!),
                    AthleteNumber = entry.Participation.Combination.Number,
                    FirstName = athlete.Names.Names.First(),
                    FamilyName = athlete.Names.Names.Last(),
                    CompetingFor = athlete.Country.NfCode!,
                },
                Horse = new ctHorse { FEIID = horse.FeiId!, Name = horse.Name },
                Complement = new ctEnduranceComplement { BestCondition = false },
                Position = new ctPositionIndiv { Status = entry.Participation.Eliminated?.Code ?? "R" },
            };
            if (ctParticipation.Position.Status == "R")
            {
                ctParticipation.Position.Rank = entry.Rank!.Value;
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

    ctEnduranceTotal CreateTotal(Participation participation)
    {
        var total = participation.GetTotal();
        var time = total?.Interval.ToTimeSpan() ?? TimeSpan.Zero;
        var speed = total?.AverageSpeed.ToDouble() ?? default;
        return new ctEnduranceTotal { AverageSpeed = Round(speed), Time = FormatTime(time) };
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
                    PhaseTime = FormatTime(phaseInterval),
                    RecoveryTime = FormatTime(recoveryInterval),
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
        if (participation.Eliminated != null)
        {
            var eliminationCode = participation.Eliminated.Code;
            if (participation.Eliminated is FailedToQualify failedToQualify)
            {
                var codes = failedToQualify.FtqCodes.Select(x => x.ToString());
                eliminationCode = string.Join(" ", codes);
            }
            var ctPhase = days.Last().Phase.Last();
            ctPhase.VetInspection = new ctEnduranceVetInspection
            {
                Type = stEnduranceVetTypeCode.Standard,
                EliminationCode = eliminationCode,
            };
        }

        return days;
    }

    string GetFeiCategory(AthleteCategory category)
    {
        return category switch
        {
            AthleteCategory.Senior => "S",
            AthleteCategory.Children => "C",
            AthleteCategory.JuniorOrYoungAdult => "YJ",
            AthleteCategory.Training or AthleteCategory.Companion or _ => throw GuardHelper.Exception(
                "Implement validation for non-fei categories for Star during setup"
            ), //TODO:
        };
    }

    string Serialize(HorseSport horseSport)
    {
        var serializer = new XmlSerializer(typeof(HorseSport));
        using var stream = new StringWriter();
        serializer.Serialize(stream, horseSport);
        return stream.ToString();
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

    string FormatTime(TimeSpan value)
    {
        return value.ToString(@"hh\:mm\:ss");
    }
}

public interface IFeiExportBusiness : ITransient
{
    Task<string> Create(Ranklist ranklist);
}
