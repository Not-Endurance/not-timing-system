using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Domain.Aggregates;

namespace NTS.Tools.Developer;

/// <summary>What a command did, or why it did not.</summary>
public enum DeveloperOutcome
{
    /// <summary>A dry run: the command would change the data.</summary>
    WouldChange = 1,
    Changed = 2,

    /// <summary>The data is already as the command would leave it, so nothing was written.</summary>
    AlreadyDone = 3,
    AccountNotFound = 4,
    TenantNotFound = 5,
}

/// <summary>The outcome of a command and what it tells the person who ran it.</summary>
public sealed class DeveloperResult
{
    public DeveloperResult(DeveloperOutcome outcome, string message, int tenantRoots = 0)
    {
        Outcome = outcome;
        Message = message;
        TenantRoots = tenantRoots;
    }

    public DeveloperOutcome Outcome { get; }
    public string Message { get; }

    /// <summary>For a Tenant Root seeded: how many accounts are Tenant Roots of the Tenant now. The Tenant is operational when there is one.</summary>
    public int TenantRoots { get; }

    /// <summary>Whether the command was refused, which is not the same as having nothing to do.</summary>
    public bool Refused => Outcome is DeveloperOutcome.AccountNotFound or DeveloperOutcome.TenantNotFound;
}

/// <summary>
/// What only the Developer does, and only by command (ADR-0012): seed a Tenant Root, which makes the Tenant operational,
/// and grant Developer. No route of the Api does either, so neither can be reached by anyone who is merely signed in.
/// Both work on the user documents the Api keeps (<c>Memberships</c> and <c>IsDeveloper</c>) by changing only that, in one
/// atomic update, so running one twice changes nothing and nothing else of an account is touched. The account has to exist
/// already: a person registers first, and the Developer names them by their exact email.
/// </summary>
public static class DeveloperCommands
{
    const string USERS = "users";
    const string TENANTS = "tenants";
    const string MEMBERSHIPS = "Memberships";
    const string IS_DEVELOPER = "IsDeveloper";

    /// <summary>
    /// Makes the account a Tenant Root of the Tenant, by adding the role to its Membership there, or a Membership that has
    /// it when the account has none in the Tenant. The Tenant has to exist: it is made when the first person of its country
    /// registers.
    /// </summary>
    public static async Task<DeveloperResult> SeedTenantRoot(
        IMongoDatabase database,
        string tenantId,
        string email,
        bool apply
    )
    {
        var users = database.GetCollection<BsonDocument>(USERS);
        var tenant = await database
            .GetCollection<BsonDocument>(TENANTS)
            .Find(new BsonDocument("_id", tenantId))
            .FirstOrDefaultAsync();
        if (tenant == null)
        {
            return new DeveloperResult(
                DeveloperOutcome.TenantNotFound,
                $"There is no Tenant '{tenantId}': a Tenant is made when the first person of its country registers."
            );
        }

        var account = await FindAccountAsync(users, email);
        if (account == null)
        {
            return new DeveloperResult(
                DeveloperOutcome.AccountNotFound,
                "There is no account with that email: the person registers first, and then the command is run again."
            );
        }

        var name = tenant.GetValue("Name", tenantId).ToString();
        var membership = MembershipOf(account, tenantId);
        if (HoldsTenantRoot(membership))
        {
            return new DeveloperResult(
                DeveloperOutcome.AlreadyDone,
                $"The account is already a Tenant Root of {name}.",
                await TenantRootsAsync(users, tenantId)
            );
        }

        if (!apply)
        {
            return new DeveloperResult(
                DeveloperOutcome.WouldChange,
                $"Would make the account a Tenant Root of {name}, which makes {name} operational.",
                await TenantRootsAsync(users, tenantId)
            );
        }

        var id = account["_id"];
        if (membership != null)
        {
            await users.UpdateOneAsync(
                new BsonDocument { { "_id", id }, { $"{MEMBERSHIPS}.TenantId", tenantId } },
                Builders<BsonDocument>.Update.AddToSet($"{MEMBERSHIPS}.$.Roles", Membership.TENANT_ROOT)
            );
        }
        else
        {
            await users.UpdateOneAsync(
                new BsonDocument { { "_id", id }, { $"{MEMBERSHIPS}.TenantId", new BsonDocument("$ne", tenantId) } },
                Builders<BsonDocument>.Update.Push(
                    MEMBERSHIPS,
                    new BsonDocument
                    {
                        { "TenantId", tenantId },
                        {
                            "Roles",
                            new BsonArray { Membership.TENANT_ROOT }
                        },
                    }
                )
            );
        }

        var roots = await TenantRootsAsync(users, tenantId);
        return new DeveloperResult(
            DeveloperOutcome.Changed,
            $"The account is a Tenant Root of {name}, which is operational: it has {roots} Tenant Root(s).",
            roots
        );
    }

    /// <summary>Sets <c>IsDeveloper</c> on the account. The Developer is a role of the platform and belongs to no Tenant.</summary>
    public static async Task<DeveloperResult> GrantDeveloper(IMongoDatabase database, string email, bool apply)
    {
        var users = database.GetCollection<BsonDocument>(USERS);
        var account = await FindAccountAsync(users, email);
        if (account == null)
        {
            return new DeveloperResult(
                DeveloperOutcome.AccountNotFound,
                "There is no account with that email: the person registers first, and then the command is run again."
            );
        }

        if (account.TryGetValue(IS_DEVELOPER, out var flag) && flag.IsBoolean && flag.AsBoolean)
        {
            return new DeveloperResult(DeveloperOutcome.AlreadyDone, "The account is already the Developer.");
        }

        if (!apply)
        {
            return new DeveloperResult(DeveloperOutcome.WouldChange, "Would grant Developer to the account.");
        }

        await users.UpdateOneAsync(
            new BsonDocument("_id", account["_id"]),
            Builders<BsonDocument>.Update.Set(IS_DEVELOPER, true)
        );
        return new DeveloperResult(DeveloperOutcome.Changed, "The account is the Developer.");
    }

    /// <summary>The account with the email, however it is cased: accounts keep it in lower case, and a person types it as they like.</summary>
    static Task<BsonDocument?> FindAccountAsync(IMongoCollection<BsonDocument> users, string email)
    {
        var typed = email.Trim();
        var caseInsensitive = new FindOptions
        {
            Collation = new Collation("en", strength: CollationStrength.Secondary),
        };
        return users.Find(new BsonDocument("Email", typed), caseInsensitive).FirstOrDefaultAsync()!;
    }

    /// <summary>Whether the Membership has the role. One with no roles member, as a hand-made document may be, has none.</summary>
    static bool HoldsTenantRoot(BsonDocument? membership)
    {
        return membership != null
            && membership.TryGetValue("Roles", out var roles)
            && roles.IsBsonArray
            && roles.AsBsonArray.Contains(Membership.TENANT_ROOT);
    }

    static BsonDocument? MembershipOf(BsonDocument account, string tenantId)
    {
        if (!account.TryGetValue(MEMBERSHIPS, out var memberships) || !memberships.IsBsonArray)
        {
            return null;
        }

        return memberships
            .AsBsonArray.Where(x => x.IsBsonDocument)
            .Select(x => x.AsBsonDocument)
            .FirstOrDefault(x => x.GetValue("TenantId", BsonNull.Value) == tenantId);
    }

    static async Task<int> TenantRootsAsync(IMongoCollection<BsonDocument> users, string tenantId)
    {
        var roots = await users.CountDocumentsAsync(
            new BsonDocument(
                MEMBERSHIPS,
                new BsonDocument(
                    "$elemMatch",
                    new BsonDocument { { "TenantId", tenantId }, { "Roles", Membership.TENANT_ROOT } }
                )
            )
        );
        return (int)roots;
    }
}
