using Not.Domain.Exceptions;
using Not.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using static NTS.Localization.NtsStrings;

namespace NTS.Tests.Unit.Harness;

internal sealed class ParticipationTimelineForm : IPhaseState
{
    public ParticipationTimelineForm(Guid id, DateTimeOffset? startTime, DateTimeOffset? arriveTime, DateTimeOffset? presentTime)
    {
        Id = id;
        StartTime = startTime;
        ArriveTime = arriveTime;
        PresentTime = presentTime;
    }

    public Guid Id { get; }
    public DateTimeOffset? StartTime { get; }
    public DateTimeOffset? ArriveTime { get; }
    public DateTimeOffset? PresentTime { get; }
    public DateTimeOffset? RepresentTime => null;
}
