using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Services.StartValidation;
using SetupAthlete = NTS.Domain.Setup.Aggregates.Athlete;
using SetupCombination = NTS.Domain.Setup.Aggregates.ConfigureEvents.Combination;
using SetupCompetition = NTS.Domain.Setup.Aggregates.ConfigureEvents.Competition;
using SetupConfigureEvent = NTS.Domain.Setup.Aggregates.ConfigureEvent;
using SetupHorse = NTS.Domain.Setup.Aggregates.Horse;
using SetupParticipation = NTS.Domain.Setup.Aggregates.ConfigureEvents.Participation;

namespace NTS.Tests.Unit.Harness;

internal sealed record FeiValues
{
    public static FeiValues All { get; } =
        new()
        {
            EventId = "FEI-EVENT",
            EventCode = "CEI1",
            CompetitionId = "FEI-COMPETITION",
            Rule = "Rule",
            ScheduleNumber = "1",
        };

    public static FeiValues None { get; } = new();

    public string? EventId { get; init; }
    public string? EventCode { get; init; }
    public string? CompetitionId { get; init; }
    public string? Rule { get; init; }
    public string? ScheduleNumber { get; init; }

    public FeiValues With(FeiExportValue value, string? text)
    {
        return value switch
        {
            FeiExportValue.EventId => this with { EventId = text },
            FeiExportValue.EventCode => this with { EventCode = text },
            FeiExportValue.CompetitionId => this with { CompetitionId = text },
            FeiExportValue.Rule => this with { Rule = text },
            FeiExportValue.ScheduleNumber => this with { ScheduleNumber = text },
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }
}
