using NC.DateAndTime;
using NC.Domain.Exceptions;
using NC.Krud.Models;
using NoTiming.Domain.Objects;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Judge.Contracts.Features.Setup.ConfigureEvents.Competitions;

public record CompetitionFormModel : KrudFormModel<Competition>
{
    int? _requiredInspectionCompulsoryThreshold;

    public CompetitionFormModel()
    {
        UseCompulsoryThreshold = true;
        CompulsoryThresholdMinutes = 10;
#if DEBUG
        Name = "Olympic Games";
#endif
    }

    public string? Name { get; set; }
    public bool EnglishNames { get; set; }
    public DateTime? Date { get; set; } = DateTime.Now;
    public TimeSpan? Time { get; set; } = DateTime.Now.TimeOfDay;
    public bool UseCompulsoryThreshold { get; set; }
    public int? CompulsoryThresholdMinutes
    {
        get => UseCompulsoryThreshold ? _requiredInspectionCompulsoryThreshold : null;
        set => _requiredInspectionCompulsoryThreshold = value;
    }
    public double? LoopSpeedMin { get; set; }
    public double? LoopSpeedMax { get; set; }
    public double? PhaseSpeedMin { get; set; }
    public double? PhaseSpeedMax { get; set; }
    public double? LoopAverageSpeedMin { get; set; }
    public double? LoopAverageSpeedMax { get; set; }
    public double? PhaseAverageSpeedMin { get; set; }
    public double? PhaseAverageSpeedMax { get; set; }
    public string? FeiEventId { get; set; }
    public string? FeiEventCode { get; set; }
    public string? FeiCompetitionId { get; set; }
    public string? FeiRule { get; set; }
    public string? FeiScheduleNumber { get; set; }
    public IReadOnlyCollection<Phase> Phases { get; private set; } = [];
    public IReadOnlyCollection<Participation> Participations { get; private set; } = [];

    protected override Competition MapTo()
    {
        var startTime = ConvertStartTime(Date, Time);
        var compulsoryThreshold = ConvertMinutes(CompulsoryThresholdMinutes);
        return new Competition(
            Name,
            EnglishNames,
            startTime,
            compulsoryThreshold,
            new SpeedRestrictions(
                LoopSpeedMin,
                LoopSpeedMax,
                PhaseSpeedMin,
                PhaseSpeedMax,
                LoopAverageSpeedMin,
                LoopAverageSpeedMax,
                PhaseAverageSpeedMin,
                PhaseAverageSpeedMax
            ),
            FeiEventId,
            FeiEventCode,
            FeiCompetitionId,
            FeiRule,
            FeiScheduleNumber,
            Phases,
            Participations,
            Id
        );
    }

    public override void MapFrom(Competition competition)
    {
        Id = competition.Id;
        Name = competition.Name;
        Date = competition.Start.ToLocalTime().DateTime.Date; // TODO: Create NComponent that's using DateTimeOffset and convert to DateTime correctly
        Time = competition.Start.ToLocalTime().DateTime.TimeOfDay;
        EnglishNames = competition.EnglishNames;
        Phases = competition.Phases;
        Participations = competition.Participations;
        CompulsoryThresholdMinutes = competition.CompulsoryThresholdSpan?.Minutes;
        UseCompulsoryThreshold = competition.CompulsoryThresholdSpan != null;
        var restrictions = competition.Restrictions;
        LoopSpeedMin = restrictions.LoopSpeedMin;
        LoopSpeedMax = restrictions.LoopSpeedMax;
        PhaseSpeedMin = restrictions.PhaseSpeedMin;
        PhaseSpeedMax = restrictions.PhaseSpeedMax;
        LoopAverageSpeedMin = restrictions.LoopAverageSpeedMin;
        LoopAverageSpeedMax = restrictions.LoopAverageSpeedMax;
        PhaseAverageSpeedMin = restrictions.PhaseAverageSpeedMin;
        PhaseAverageSpeedMax = restrictions.PhaseAverageSpeedMax;
        FeiEventId = competition.FeiEventId;
        FeiEventCode = competition.FeiEventCode;
        FeiCompetitionId = competition.FeiCompetitionId;
        FeiRule = competition.FeiRule;
        FeiScheduleNumber = competition.FeiScheduleNumber;
    }

    TimeSpan? ConvertMinutes(int? minutes)
    {
        return minutes == null ? null : TimeSpan.FromMinutes(minutes.Value);
    }

    DateTimeOffset ConvertStartTime(DateTime? date, TimeSpan? time)
    {
        if (date == null)
        {
            throw new DomainPropertyException(nameof(CompetitionFormModel.Date), Null_or_malformed_string, Time_string);
        }
        if (time == null)
        {
            throw new DomainPropertyException(nameof(CompetitionFormModel.Time), Null_or_malformed_string, Time_string);
        }

        return date.Value.ToLocalDateTime(time.Value);
    }
}
