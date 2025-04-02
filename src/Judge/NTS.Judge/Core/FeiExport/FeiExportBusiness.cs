using Not.Application.CRUD.Ports;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;

namespace NTS.Judge.Core.FeiExport;

public class FeiExportBusiness : IFeiExportBusiness
{
    readonly IRepository<EnduranceEvent> _events;

    public FeiExportBusiness(IRepository<EnduranceEvent> events)
    {
        _events = events;
    }
    
    public async Task Export(Ranklist ranklist)
    {
        var enduranceEvent = await _events.Read(0);
        
        throw new NotImplementedException();
    }

    ctEnduranceCompetition CreateCompetitions(Competition competition, string showFeiId, Category category)
    {
        var categoryString = category == Category.Seniors
            ? "S"
            : category == Category.Children ? "C" : "YJ";
        var competitionFeiId = $"{showFeiId}_E_{categoryString}_{competition.FeiCategoryEventNumber}_{competition.FeiScheduleNumber}";
        var ctCompetition = new ctEnduranceCompetition
        {
            FEIID = competitionFeiId,
            ScheduleCompetitionNr = competition.FeiScheduleNumber,
            Rule = competition.Rule,
            Name = competition.Name,
            StartDate = competition.StartTime,
            Team = false,
            ParticipationList = new ctEnduranceParticipations()
        };

        var ctParticipations = CreateParticipations(competition);

        // .. Necessary to order here, because Ranklist implementation is terrible
        ctCompetition.ParticipationList.Participation = ctParticipations.OrderBy(x => x.Position.Rank).ToArray();
        return ctCompetition;
    }

    IEnumerable<ctEnduranceIndivResult> CreateParticipations(Competition competition)
    {
        var competitionResult = _competitions.First(x => x.Id == competition.Id);
        var participations = _stateContext.State.Participations.Where(x => x.CompetitionsIds.Contains(competition.Id));
        var withoutFeiId = participations.Where(x => string.IsNullOrWhiteSpace(x.Participant.Athlete.FeiId));
        if (withoutFeiId.Any())
        {
            var numbers = string.Join(", ", withoutFeiId.Select(x => x.Participant.Number));
            throw new DomainException(nameof(Participant), $"Participants '{numbers}' are not configured with Athlete FEIID");
        }
        foreach (var participation in participations)
        {
            var ranklist = competitionResult.Rank(participation.Participant.Athlete.Category);
            var result = ranklist.FirstOrDefault(x => x.Participant.Number == participation.Participant.Number);
            var athlete = participation.Participant.Athlete;
            var horse = participation.Participant.Horse;

            var ctParticipation = new ctEnduranceIndivResult
            {
                Athlete = new ctEnduranceAthlete
                {
                    FEIID = int.Parse(athlete.FeiId),
                    AthleteNumber = int.Parse(participation.Participant.Number),
                    FirstName = athlete.FirstName,
                    FamilyName = athlete.LastName,
                    CompetingFor = athlete.Country.IsoCode,
                },
                Horse = new ctHorse
                {
                    FEIID = horse.FeiId,
                    Name = horse.Name
                },
                Complement = new ctEnduranceComplement
                {
                    BestCondition = false,
                },
                Position = new ctPositionIndiv
                {
                    Status = result.Participant.LapRecords.Last().Result.TypeCode,
                    Rank = ranklist.IndexOf(result) + 1,
                },
            };

            var ctDays = CreateDaysAndPhases(participation, competition);
            ctParticipation.Phases = ctDays.ToArray();

            if (participation.Participant.LapRecords.All(x => x.Result.Type == ResultType.Successful))
            {
                ctParticipation.Total = CreateTotal(participation);
            }

            yield return ctParticipation;
        }
    }

    ctEnduranceTotal CreateTotal(Participation participation)
    {
        var total = participation.GetTotal();
        var time = total?.Interval.ToTimeSpan();
        var speed = total?.AverageSpeed.ToDouble()  ?? default;
        return new ctEnduranceTotal
        {
            AverageSpeed = Round(speed),
            Time = FormatTime(time),
        };
    }

    IEnumerable<ctEnduranceDayResult> CreateDaysAndPhases(Participation participation, Competition competition)
    {
        var days = new List<ctEnduranceDayResult>();
        var day = new ctEnduranceDayResult() { Number = 1 };
        var lastDate = default(DateTime);
        foreach (var record in participation.Participant.LapRecords)
        {
            var performance = new Performance(record, competition.Type, 0);
            var phase = new ctEndurancePhaseResult
            {
                Number = participation.Participant.LapRecords.IndexOf(record) + 1,
                Result = new ctEndurancePhaseResultScore
                {
                    PhaseAverageSpeed = Round(performance.AverageSpeedPhase.Value),
                    PhaseTime = FormatTime(performance.Time),
                    RecoveryTime = FormatTime(performance.RecoverySpan),
                },
            };
            if (record.Result.Type != ResultType.Successful)
            {
                var eliminationCode = record.Result.TypeCode == "RET"
                    ? record.Result.TypeCode
                    : $"{record.Result.TypeCode} {record.Result.Code}";
                phase.VetInspection = new ctEnduranceVetInspection
                {
                    Type = stEnduranceVetTypeCode.Standard,
                    EliminationCode = eliminationCode,
                };
            }
            if (lastDate == default || lastDate == record.StartTime.Date)
            {
                var list = new List<ctEndurancePhaseResult>(day.Phase ?? Enumerable.Empty<ctEndurancePhaseResult>())
                {
                    phase
                };
                day.Phase = list.ToArray();
            }
            else
            {
                days.Add(day);
                day = new ctEnduranceDayResult()
                {
                    Phase = new List<ctEndurancePhaseResult> { phase }.ToArray(),
                    Number = day.Number + 1
                };
            }
            lastDate = record.StartTime.Date;
        }
        days.Add(day);
        return days;
    }

    HorseSport CreateHorseSport(EnduranceEvent @event, ctEnduranceCompetition ctEnduranceCompetition, Category category, Competition competition)
    {
        if (string.IsNullOrWhiteSpace(@event.ShowFeiId))
        {
            throw new DomainException(nameof(EnduranceEvent), "Missing Show FEIID");
        }
        if (string.IsNullOrEmpty(@event.PopulatedPlace))
        {
            throw new DomainException(nameof(EnduranceEvent), "Missing PopulatedPlace");
        }
        if (string.IsNullOrEmpty(competition.Name))
        {
            throw new DomainException(nameof(Competition), "Missing Name");
        }
        if (string.IsNullOrEmpty(competition.FeiCategoryEventNumber))
        {
            throw new DomainException(nameof(Competition), "Missing FEI Category Event NR");
        }
        if (string.IsNullOrEmpty(competition.FeiScheduleNumber))
        {
            throw new DomainException(nameof(Competition), "Missing FEI Schedule NR");
        }
        if (string.IsNullOrEmpty(competition.Rule))
        {
            throw new DomainException(nameof(Competition), "Missing FEI Rule");
        }
        if (string.IsNullOrEmpty(competition.EventCode))
        {
            throw new DomainException(nameof(Competition), "Missing FEI Event Code");
        }

        var categoryString = category == Category.Seniors
            ? "S"
            : category == Category.Children ? "C" : "YJ";
        var competitionFeiId = $"{@event.ShowFeiId}_E_{categoryString}_{competition.FeiCategoryEventNumber}";
        var ctEnduranceEvent = new ctEnduranceEvent
        {
            FEIID = competitionFeiId,
            Code = competition.EventCode,
            StartDate = @event.Competitions.OrderBy(x => x.StartTime).First().StartTime,
            EndDate = DateTime.UtcNow,
            NF = @event.Country.IsoCode,
            Competitions = new ctEnduranceCompetition[] { ctEnduranceCompetition },
        };
        var horseSport = new HorseSport()
        {
            Generated = new ctGenerated
            {
                Software = "EMS",
                SoftwareVersion = "4.1.3",
                Organization = "NotACompany",
            },
            EventResult = new ctShowResultType
            {
                Show = new ctShowResult
                {
                    Venue = new ctVenue
                    {
                        Name = @event.PopulatedPlace,
                        Country = @event.Country.IsoCode,
                    },
                    EnduranceEvent = new List<ctEnduranceEvent> { ctEnduranceEvent }.ToArray(),
                    StartDate = @event.Competitions.OrderBy(x => x.StartTime).First().StartTime.Date,
                    EndDate = DateTime.UtcNow.Date,
                    FEIID = @event.ShowFeiId,
                }
            }
        };
        return horseSport;
    }

    string BuildXml(HorseSport horseSport)
    {
        var serializer = new XmlSerializer(typeof(HorseSport));
        using var stream = new StringWriter();
        serializer.Serialize(stream, horseSport);
        return stream.ToString();
    }

    string InsertGeneratedDate(string xml)
    {
        return xml.Replace("<Generated Software", $"<Generated Date=\"{DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture)}+00:00\" Software");
    }

    decimal Round(double value) => (decimal)Math.Round(value, 2);

    string FormatTime(TimeSpan? value) => value?.ToString(@"hh\:mm\:ss");
        
    
    
    
    
}

public interface IFeiExportBusiness
{
    Task Export(Ranklist ranklist);
}
