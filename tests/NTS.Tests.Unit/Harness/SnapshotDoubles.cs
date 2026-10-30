using Not.Domain.Exceptions;
using NoTiming.Ui.Features.Core.Dashboard;
using NoTiming.Ui.Features.Core.Participations;
using NoTiming.Ui.Features.Sessions;
using NTS.Contracts.Features.Access;
using NTS.Contracts.Features.Snapshots;
using NTS.Contracts.Watcher.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using static NTS.Localization.NtsStrings;
using NTS.Domain.Core.Aggregates.Participations.Events;
using static NTS.Tests.Unit.Harness.ViewedEventFixtures;

namespace NTS.Tests.Unit.Harness;

internal static class SnapshotDoubles
{
    public static RecordedSnapshot Recorded(TimeEventOutcome outcome)
    {
        return new RecordedSnapshot
        {
            EventId = LIVE_EVENT_ID,
            ParticipationId = TestId.Of(1),
            Number = 1,
            Slot = TimeSlot.Arrive,
            Time = DateTimeOffset.Now,
            Outcome = outcome,
        };
    }
}
