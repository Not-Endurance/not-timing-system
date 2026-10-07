using MongoDB.Bson;
using NTS.Domain.Access;

namespace NTS.Tools.Shared;

/// <summary>
/// How the commands that prepare a database read and write its documents (#607): as they are stored, in BSON, so that what a
/// command does not know of a document is kept. Accounts and grants are written in the form the Api reads them in; the
/// integration tests read what the commands wrote with the Api's own readers, so the two cannot drift apart unnoticed.
/// </summary>
internal static class Records
{
    public const string TENANT_ID = "TenantId";
    public const string MEMBERSHIPS = "Memberships";
    public const string HOME_TENANT = "HomeTenantId";

    public static BsonBinaryData Uuid(Guid id)
    {
        return new BsonBinaryData(id, GuidRepresentation.Standard);
    }

    /// <summary>The id a field holds when it is a UUID of the standard kind, none otherwise: Identity reads no other.</summary>
    public static Guid? GuidOf(BsonDocument document, string field)
    {
        return
            document.TryGetValue(field, out var value)
            && value is BsonBinaryData { SubType: BsonBinarySubType.UuidStandard } binary
            && binary.ToGuid(GuidRepresentation.Standard) is var guid
            && guid != Guid.Empty
            ? guid
            : null;
    }

    public static string? TextOf(BsonDocument document, string field, bool trim = true)
    {
        return
            document.TryGetValue(field, out var value) && value.IsString && !string.IsNullOrWhiteSpace(value.AsString)
            ? trim
                ? value.AsString.Trim()
                : value.AsString
            : null;
    }

    public static bool HasValue(BsonDocument document, string field)
    {
        return TextOf(document, field) != null;
    }

    /// <summary>The email as accounts and grants are matched by it: without the spaces around it, in lower case.</summary>
    public static string? NormalEmail(string? typed)
    {
        return string.IsNullOrWhiteSpace(typed) ? null : typed.Trim().ToLowerInvariant();
    }

    public static BsonDocument NewMembership(string tenant, params string[] roles)
    {
        return new BsonDocument { { TENANT_ID, tenant }, { "Roles", new BsonArray(roles) } };
    }

    public static bool HasMembership(BsonDocument account, string tenant)
    {
        return account.TryGetValue(MEMBERSHIPS, out var memberships)
            && memberships.IsBsonArray
            && memberships.AsBsonArray.Any(x =>
                x.IsBsonDocument && x.AsBsonDocument.GetValue(TENANT_ID, BsonNull.Value) == tenant
            );
    }

    /// <summary>A grant as a document of the <c>event_grants</c> collection, which is the shape the Api reads (<c>GrantDocuments</c>).</summary>
    public static BsonDocument GrantDocument(EventGrant grant)
    {
        var document = new BsonDocument
        {
            { "_id", Uuid(grant.Id) },
            { TENANT_ID, grant.TenantId },
            { "EventId", Uuid(grant.EventId) },
            { "Kind", grant.Kind.ToString() },
            { "Email", grant.Email },
        };
        if (grant.OfficialRole is { } role)
        {
            document["OfficialRole"] = role.ToString();
        }

        if (grant.AccountId is { } account)
        {
            document["AccountId"] = Uuid(account);
        }

        return document;
    }

    /// <summary>What makes a grant the same grant: the Event, the kind, the role of an Official, and the email.</summary>
    public static string GrantKeyOf(BsonDocument grant)
    {
        return string.Join(
            "|",
            GuidOf(grant, "EventId"),
            TextOf(grant, "Kind"),
            TextOf(grant, "OfficialRole"),
            TextOf(grant, "Email", trim: false)
        );
    }
}
