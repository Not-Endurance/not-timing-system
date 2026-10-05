using System.Text;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.Features.UserSessions;
using NoTiming.Api.JsonApi;
using NTS.Application.Factories;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Services.StartValidation;
using static NTS.Localization.NtsStrings;
using SetupCompetition = NTS.Domain.Setup.Aggregates.ConfigureEvents.Competition;

namespace NoTiming.Api.Features.Events;

/// <summary>
/// What starting an Event makes from its Setup (ADR-0006, #628): the Core document of the Event, and beside it the copies
/// of its Officials and Operators and the Participations and Rankings of its competitions, each a document with the id of
/// the Event. Nothing is written until the plan is whole.
/// </summary>
internal sealed class EventStartPlan
{
    public EventStartPlan(
        EventInformationModel eventInformation,
        IReadOnlyList<(string Collection, IReadOnlyList<BsonDocument> Documents)> children
    )
    {
        EventInformation = eventInformation;
        Children = children;
    }

    public EventInformationModel EventInformation { get; }

    public IReadOnlyList<(string Collection, IReadOnlyList<BsonDocument> Documents)> Children { get; }
}

/// <summary>
/// The plan to start an Event from a Setup, with the validation the Event has always had before it starts: a Setup that is
/// not whole is refused with what is missing (422 <c>invalid-setup</c>), and so is one that configures the FEI export in
/// part (422 <c>incomplete-fei-configuration</c>). The rules of the Regional competitions are those of the Tenant at this
/// moment (ADR-0012).
/// </summary>
internal static class EventStartPlans
{
    public static (EventStartPlan? Plan, IResult? Refusal) Create(ConfigureEvent setup, RegionalRules rules)
    {
        var issues = StartValidator.Validate(setup).Data ?? [];
        if (issues.Count > 0)
        {
            return (
                null,
                JsonApiResults.Error(
                    StatusCodes.Status422UnprocessableEntity,
                    "invalid-setup",
                    Start_validation_invalid_setup_title_string,
                    StartValidationDetail(issues)
                )
            );
        }

        if (MissingFeiConfiguration(setup) is { } missing)
        {
            return (
                null,
                JsonApiResults.Error(
                    StatusCodes.Status422UnprocessableEntity,
                    "incomplete-fei-configuration",
                    "The FEI export is only partly configured.",
                    string.Format(Missing_FEI_export_configurations_colon__, Environment.NewLine + missing)
                )
            );
        }

        var eventInformation = EventInformationModel.From(EventInformationFactory.Create(setup, rules));
        var (participations, rankings) = CreateParticipationsAndRankings(setup);
        IReadOnlyList<(string Collection, IReadOnlyList<BsonDocument> Documents)> children =
        [
            (
                TenantOwned.EVENT_OFFICIALS,
                [
                    .. setup.Officials.Select(x =>
                        OfficialModel.MapFrom(OfficialFactory.Create(x, setup.Id)).ToBsonDocument()
                    ),
                ]
            ),
            (
                TenantOwned.EVENT_OPERATORS,
                [
                    .. setup.Operators.Select(x =>
                        OperatorModel.MapFrom(OperatorFactory.Create(x, setup.Id)).ToBsonDocument()
                    ),
                ]
            ),
            (
                TenantOwned.EVENT_PARTICIPATIONS,
                [.. participations.Select(x => ParticipationModel.MapFrom(x).ToBsonDocument())]
            ),
            (TenantOwned.EVENT_RANKINGS, [.. rankings.Select(x => RankingModel.From(x).ToBsonDocument())]),
        ];
        return (new EventStartPlan(eventInformation, children), null);
    }

    static string StartValidationDetail(IReadOnlyList<StartValidationIssue> issues)
    {
        var detail = new StringBuilder().AppendLine(Start_validation_invalid_setup_description_string);
        foreach (var issue in issues)
        {
            detail.AppendLine(issue.Summary);
            if (!issue.IsAutoCorrectable)
            {
                continue;
            }

            foreach (var competition in issue.Competitions)
            {
                detail.AppendLine($"- {competition.CompetitionName}: {competition.PhaseSignature}");
            }
        }

        return detail.ToString().TrimEnd();
    }

    static (
        IReadOnlyList<Participation> Participations,
        IReadOnlyList<Ranking> Rankings
    ) CreateParticipationsAndRankings(ConfigureEvent setup)
    {
        var participations = new List<Participation>();
        var rankings = new List<Ranking>();
        foreach (var competition in setup.Competitions)
        {
            var (made, entriesByCategory) = ParticipationAndRankingFactory.Create(
                competition,
                participations,
                setup.Id
            );
            participations.AddRange(made);
            rankings.AddRange(
                entriesByCategory.Where(x => x.Value.Any()).Select(x => CreateRanking(competition, x, setup.Id))
            );
        }

        return (participations, rankings);
    }

    static Ranking CreateRanking(
        SetupCompetition competition,
        KeyValuePair<ParticipationCategory, List<RankingEntry>> entriesByCategory,
        Guid eventId
    )
    {
        return new Ranking(
            competition.Name,
            competition.Ruleset,
            entriesByCategory.Key,
            competition.FeiEventId,
            competition.FeiEventCode,
            competition.FeiCompetitionId,
            competition.FeiRule,
            competition.FeiScheduleNumber,
            entriesByCategory.Value,
            eventId
        );
    }

    /// <summary>What is missing of the FEI configuration, a line each; none when there is none of it or all of it.</summary>
    static string? MissingFeiConfiguration(ConfigureEvent setup)
    {
        if (!HasAnyFeiConfiguration(setup))
        {
            return null;
        }

        var missing = new StringBuilder();
        if (string.IsNullOrWhiteSpace(setup.FeiShowId))
        {
            missing.AppendLine(FEI_Show_ID_string);
        }

        foreach (var competition in setup.Competitions.Where(HasAnyCompetitionFeiConfiguration))
        {
            if (string.IsNullOrWhiteSpace(competition.FeiEventId))
            {
                missing.AppendLine($"{competition.Name}: {FEI_Event_ID_string}");
            }

            if (string.IsNullOrWhiteSpace(competition.FeiEventCode))
            {
                missing.AppendLine($"{competition.Name}: {FEI_Event_Code_string}");
            }

            if (string.IsNullOrWhiteSpace(competition.FeiCompetitionId))
            {
                missing.AppendLine($"{competition.Name}: {FEI_Competition_ID_string}");
            }

            if (string.IsNullOrWhiteSpace(competition.FeiRule))
            {
                missing.AppendLine($"{competition.Name}: {FEI_Rule_string}");
            }

            if (string.IsNullOrWhiteSpace(competition.FeiScheduleNumber))
            {
                missing.AppendLine($"{competition.Name}: {FEI_Schedule_Number_string}");
            }

            foreach (var participation in competition.Participations)
            {
                if (string.IsNullOrWhiteSpace(participation.Combination.Horse.FeiId))
                {
                    missing.AppendLine(
                        $"#{participation.Combination.Number}, {participation.Combination.Horse.Name}: {FEI_ID_string}"
                    );
                }

                if (string.IsNullOrWhiteSpace(participation.Combination.Athlete.FeiId))
                {
                    missing.AppendLine(
                        $"#{participation.Combination.Number}, {participation.Combination.Athlete.Name}: {FEI_ID_string}"
                    );
                }
            }
        }

        var text = missing.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    static bool HasAnyFeiConfiguration(ConfigureEvent setup)
    {
        return !string.IsNullOrWhiteSpace(setup.FeiShowId) || setup.Competitions.Any(HasAnyCompetitionFeiConfiguration);
    }

    static bool HasAnyCompetitionFeiConfiguration(SetupCompetition competition)
    {
        return !string.IsNullOrWhiteSpace(competition.FeiEventId)
            || !string.IsNullOrWhiteSpace(competition.FeiEventCode)
            || !string.IsNullOrWhiteSpace(competition.FeiCompetitionId)
            || !string.IsNullOrWhiteSpace(competition.FeiRule)
            || !string.IsNullOrWhiteSpace(competition.FeiScheduleNumber);
    }
}

/// <summary>
/// Starts and resets an Event (#628): starting writes the Core document of the Event, which is what makes it started, and
/// then what it copies and creates; resetting removes all of that, the Core document last, so that an Event that is left
/// half reset can be reset again, and a start that fails part way leaves no Event behind. Both stay inside the Event's
/// Tenant. A reset also takes the state a person kept for the Event and the Snapshots still waiting for it, which are not
/// the Tenant's documents.
/// </summary>
internal sealed class EventStarter
{
    const string PENDING_SNAPSHOTS = "event_pending_snapshots";

    readonly TenantCollections _tenants;
    readonly UserSessionStore _sessions;
    readonly IMongoCollection<BsonDocument> _pendingSnapshots;

    public EventStarter(
        TenantCollections tenants,
        UserSessionStore sessions,
        IMongoClient client,
        IOptions<NIdentityOptions> options
    )
    {
        _tenants = tenants;
        _sessions = sessions;
        _pendingSnapshots = client.GetDatabase(options.Value.Database).GetCollection<BsonDocument>(PENDING_SNAPSHOTS);
    }

    /// <summary>Starts the Event; false when it has been started already, by whoever got there first.</summary>
    public async Task<bool> StartAsync(EventStartPlan plan, string tenantId, CancellationToken cancellationToken)
    {
        try
        {
            await _tenants
                .Of(TenantOwned.EVENT_INFORMATIONS, tenantId)
                .InsertAsync(plan.EventInformation.ToBsonDocument(), cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }

        try
        {
            foreach (var (collection, documents) in plan.Children)
            {
                await _tenants.Of(collection, tenantId).InsertManyAsync(documents, cancellationToken);
            }
        }
        catch
        {
            await ResetAsync(plan.EventInformation.Id, tenantId, CancellationToken.None);
            throw;
        }

        return true;
    }

    /// <summary>Removes everything the Event made and the Event itself, which is the Setup alone again.</summary>
    public async Task ResetAsync(Guid eventId, string tenantId, CancellationToken cancellationToken)
    {
        var ofTheEvent = new BsonDocument("EventId", BsonGuids.Binary(eventId));
        foreach (var collection in TenantOwned.OfAnEvent)
        {
            await _tenants.Of(collection, tenantId).DeleteManyAsync(ofTheEvent, cancellationToken);
        }

        await _sessions.DeleteOfEventAsync(eventId, cancellationToken);
        await _pendingSnapshots.DeleteManyAsync(ofTheEvent, cancellationToken);
        await _tenants
            .Of(TenantOwned.EVENT_INFORMATIONS, tenantId)
            .DeleteAsync(new BsonDocument("_id", BsonGuids.Binary(eventId)), cancellationToken);
    }
}
