using Not.DateAndTime;
using Not.Domain.Exceptions;
using Not.Krud.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Judge.Contracts.Features.Setup.ConfigureEvents.Competitions;

public record CompetitionFormModel : KrudFormModel<Competition>
{
    int? _requiredInspectionCompulsoryThreshold;
    double? _minSpeedRestriction;
    double? _maxSpeedRestriction;

    public CompetitionFormModel()
    {
        UseCompulsoryThreshold = true;
        CompulsoryThresholdMinutes = 10;
#if DEBUG
        Name = "Olympic Games";
#endif
    }

    public string? Name { get; set; }
    public CompetitionRuleset Ruleset { get; set; } = CompetitionRuleset.Regional;
    public DateTime? Date { get; set; } = DateTime.Now;
    public TimeSpan? Time { get; set; } = DateTime.Now.TimeOfDay;
    public bool UseCompulsoryThreshold { get; set; }
    public int? CompulsoryThresholdMinutes
    {
        get => UseCompulsoryThreshold ? _requiredInspectionCompulsoryThreshold : null;
        set => _requiredInspectionCompulsoryThreshold = value;
    }
    public bool UseMinSpeedRestriction { get; set; }
    public double? MinSpeedRestriction
    {
        get => UseMinSpeedRestriction ? _minSpeedRestriction : null;
        set => _minSpeedRestriction = value;
    }
    public bool UseMaxSpeedRestriction { get; set; }
    public double? MaxSpeedRestriction
    {
        get => UseMaxSpeedRestriction ? _maxSpeedRestriction : null;
        set => _maxSpeedRestriction = value;
    }
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
            Ruleset,
            startTime,
            compulsoryThreshold,
            MinSpeedRestriction,
            MaxSpeedRestriction,
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
        Ruleset = competition.Ruleset;
        Phases = competition.Phases;
        Participations = competition.Participations;
        CompulsoryThresholdMinutes = competition.CompulsoryThresholdSpan?.Minutes;
        UseCompulsoryThreshold = competition.CompulsoryThresholdSpan != null;
        MinSpeedRestriction = competition.MinSpeedRestriction;
        UseMinSpeedRestriction = competition.MinSpeedRestriction != null;
        MaxSpeedRestriction = competition.MaxSpeedRestriction;
        UseMaxSpeedRestriction = competition.MaxSpeedRestriction != null;
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
