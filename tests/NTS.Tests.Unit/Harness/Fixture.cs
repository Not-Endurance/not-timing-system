using Not.Domain.Exceptions;
using NoTiming.Ui.Features.Core.EventViews;
using NoTiming.Ui.Features.Core.Participations;
using NTS.Contracts.Features.Access;
using NTS.Domain.Access;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Enums;
using static NTS.Tests.Unit.Harness.ViewedEventFixtures;

namespace NTS.Tests.Unit.Harness;

internal sealed class Fixture
{
    public Fixture(
        ViewedEventProvider provider,
        RecordingSocketService socket,
        ParticipationStore store,
        ControlledParticipationRepository repository,
        AccessOf access
    )
    {
        Provider = provider;
        Socket = socket;
        Store = store;
        Repository = repository;
        Access = access;
    }

    public ViewedEventProvider Provider { get; }
    public RecordingSocketService Socket { get; }
    public ParticipationStore Store { get; }
    public ControlledParticipationRepository Repository { get; }
    public AccessOf Access { get; }
}
