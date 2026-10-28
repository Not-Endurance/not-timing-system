using Not.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Harness;

internal sealed class CorrectedPhase : IPhaseState
{
    public CorrectedPhase(Guid id, DateTimeOffset startTime, DateTimeOffset arriveTime, DateTimeOffset presentTime)
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
