using MongoDB.Bson;
using NoTiming.Api.Features.Tenancy;
using NTS.Domain.Access;
using NTS.Domain.Enums;

namespace NoTiming.Api.Features.Events;

/// <summary>
/// A grant as a document of the <c>event_grants</c> collection and back. The document holds the Tenant of the Event, the
/// Event, the kind (<c>Operator</c> or <c>Official</c>) and the role of an Official as its name, the email in the form
/// grants and accounts are matched by it, and the account once there is one: a pending invitation has no
/// <c>AccountId</c> at all. A document that is not of that shape is not a grant.
/// </summary>
internal static class GrantDocuments
{
    public const string EVENT_ID = "EventId";
    public const string KIND = "Kind";
    public const string OFFICIAL_ROLE = "OfficialRole";
    public const string EMAIL = "Email";
    public const string ACCOUNT_ID = "AccountId";

    public static BsonDocument ToDocument(EventGrant grant)
    {
        var document = new BsonDocument
        {
            { "_id", BsonGuids.Binary(grant.Id) },
            { TenantOwned.TENANT_ID, grant.TenantId },
            { EVENT_ID, BsonGuids.Binary(grant.EventId) },
            { KIND, grant.Kind.ToString() },
            { EMAIL, grant.Email },
        };
        if (grant.OfficialRole is { } role)
        {
            document[OFFICIAL_ROLE] = role.ToString();
        }

        if (grant.AccountId is { } account)
        {
            document[ACCOUNT_ID] = BsonGuids.Binary(account);
        }

        return document;
    }

    public static EventGrant? ToGrant(BsonDocument document)
    {
        if (
            BsonGuids.Of(document, "_id") is not { } id
            || BsonGuids.Of(document, EVENT_ID) is not { } eventId
            || !document.TryGetValue(TenantOwned.TENANT_ID, out var tenant)
            || !tenant.IsString
            || !document.TryGetValue(EMAIL, out var email)
            || !email.IsString
            || !document.TryGetValue(KIND, out var kind)
            || !kind.IsString
            || !Enum.TryParse<GrantKind>(kind.AsString, out var grantKind)
        )
        {
            return null;
        }

        var account = BsonGuids.Of(document, ACCOUNT_ID);
        try
        {
            if (grantKind == GrantKind.Operator)
            {
                return EventGrant.ForOperator(id, eventId, tenant.AsString, email.AsString, account);
            }

            return
                document.TryGetValue(OFFICIAL_ROLE, out var role)
                && role.IsString
                && Enum.TryParse<OfficialRole>(role.AsString, out var officialRole)
                ? EventGrant.ForOfficial(id, eventId, tenant.AsString, officialRole, email.AsString, account)
                : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
