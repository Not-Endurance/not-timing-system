using NoTiming.Api.Features.Events.Factories;
using NTS.Contracts.Core.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Aggregates.Results;
using NTS.Domain.Core.Objects.Rankers;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using ConfigureEvent = NTS.Domain.Setup.Aggregates.ConfigureEvent;
using CoreCompetition = NTS.Domain.Core.Aggregates.Participations.Objects.Competition;
using CoreParticipation = NTS.Domain.Core.Aggregates.Participation;
using CorePhase = NTS.Domain.Core.Aggregates.Participations.Entities.Phase;
using SetupCompetition = NTS.Domain.Setup.Aggregates.ConfigureEvents.Competition;
using SetupLoop = NTS.Domain.Setup.Aggregates.ConfigureEvents.Loop;
using SetupPhase = NTS.Domain.Setup.Aggregates.ConfigureEvents.Phase;

namespace NTS.Tests.Unit.Harness;

internal sealed class LastNumberFirstRanker : Ranker
{
    public LastNumberFirstRanker(string code)
    {
        CountryIsoCode = code;
    }

    public override List<ParticipationResult> Rank(IEnumerable<ParticipationResult> entries)
    {
        return [.. entries.OrderByDescending(x => x.Participation.Combination.Number)];
    }
}
