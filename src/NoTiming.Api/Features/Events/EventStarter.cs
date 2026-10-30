using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.Features.UserSessions;
using NoTiming.Api.JsonApi;
using NTS.Application.Factories;
using NTS.Contracts.Core.Models;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Services.StartValidation;
using static NTS.Localization.NtsStrings;

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

        var missing = FeiExportConfiguration.MissingOf(setup);
        if (missing.Count > 0)
        {
            return (
                null,
                JsonApiResults.Error(
                    StatusCodes.Status422UnprocessableEntity,
                    "incomplete-fei-configuration",
                    "The FEI export is only partly configured.",
                    string.Format(Missing_FEI_export_configurations_colon__, Environment.NewLine + Lines(missing))
                )
            );
        }

        var documents = EventStartDocuments.From(setup, rules);
        IReadOnlyList<(string Collection, IReadOnlyList<BsonDocument> Documents)> children =
        [
            (TenantOwned.EVENT_OFFICIALS, documents.Officials),
            (TenantOwned.EVENT_OPERATORS, documents.Operators),
            (TenantOwned.EVENT_PARTICIPATIONS, documents.Participations),
            (TenantOwned.EVENT_RANKINGS, documents.Rankings),
        ];
        return (new EventStartPlan(documents.EventInformation, children), null);
    }

    static string StartValidationDetail(IReadOnlyList<StartValidationIssue> issues)
    {
        var detail = new StringBuilder().AppendLine(Start_validation_invalid_setup_description_string);
        foreach (var issue in issues)
        {
            detail.AppendLine(issue.Summary);
            foreach (var competition in issue.Competitions)
            {
                detail.AppendLine($"- {competition.CompetitionName}: {competition.PhaseSignature}");
            }
        }

        return detail.ToString().TrimEnd();
    }

    /// <summary>What is missing of the FEI configuration, in the words of the application: a line each.</summary>
    static string Lines(IReadOnlyList<MissingFeiExportValue> missing)
    {
        var lines = new StringBuilder();
        foreach (var item in missing)
        {
            var label = Label(item.Value);
            lines.AppendLine(item.Subject == null ? label : $"{item.Subject}: {label}");
        }

        return lines.ToString();
    }

    static string Label(FeiExportValue value)
    {
        return value switch
        {
            FeiExportValue.ShowId => FEI_Show_ID_string,
            FeiExportValue.EventId => FEI_Event_ID_string,
            FeiExportValue.EventCode => FEI_Event_Code_string,
            FeiExportValue.CompetitionId => FEI_Competition_ID_string,
            FeiExportValue.Rule => FEI_Rule_string,
            FeiExportValue.ScheduleNumber => FEI_Schedule_Number_string,
            FeiExportValue.FeiId => FEI_ID_string,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }
}

/// <summary>
/// Starts and resets an Event (#628): starting writes the Core document of the Event, which is what makes it started, and
/// then what it copies and creates; resetting removes all of that, the Core document last, so that an Event that is left
/// half reset can be reset again, and a start that fails part way leaves no Event behind. Both stay inside the Event's
/// Tenant. A reset also takes the state a person kept for the Event, which is not the Tenant's document.
/// </summary>
internal sealed class EventStarter
{
    readonly TenantCollections _tenants;
    readonly UserSessionStore _sessions;

    public EventStarter(TenantCollections tenants, UserSessionStore sessions)
    {
        _tenants = tenants;
        _sessions = sessions;
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
        await _tenants
            .Of(TenantOwned.EVENT_INFORMATIONS, tenantId)
            .DeleteAsync(new BsonDocument("_id", BsonGuids.Binary(eventId)), cancellationToken);
    }
}
