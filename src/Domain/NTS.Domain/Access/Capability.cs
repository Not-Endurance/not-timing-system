namespace NTS.Domain.Access;

/// <summary>
/// The actions of the access matrix of ADR-0012, one for each row of it (a row that names two actors, or two actions,
/// is split). The policy answers for one of them at a time, for a caller and the place it is done in: an Event, a
/// Tenant, or the platform.
/// </summary>
public enum Capability
{
    /// <summary>Read the public views of an Event: anyone, signed in or not.</summary>
    ReadPublicViews = 1,

    /// <summary>Read an Athlete's or a Horse's history and search the registries of every Tenant: any signed-in account.</summary>
    ReadRegistryAcrossTenants = 2,

    /// <summary>See how times were recorded: Staff, which is every Official, every Operator and the Main Operator.</summary>
    SeeTimeEvents = 3,

    /// <summary>Send a Snapshot: an Official of a role that may, any Operator, the Main Operator; to a Live Event.</summary>
    SendSnapshot = 4,

    /// <summary>Configure an Event and use the Console: the Main Operator.</summary>
    ConfigureEvent = 5,

    /// <summary>Create an Event in a Tenant: a Tenant Root of it.</summary>
    CreateEvent = 6,

    /// <summary>Assign the Main Operator while the Event is not started: a Tenant Root of its Tenant.</summary>
    AssignMainOperator = 7,

    /// <summary>Hand the Event to another account once it is Live: only the Main Operator.</summary>
    HandOverMainOperator = 8,

    /// <summary>Link accounts to the Event's Officials and Operators: the Main Operator.</summary>
    LinkAccounts = 9,

    /// <summary>
    /// Edit the Tenant's registry: a Tenant Root, and the Main Operator of an Event of the Tenant that is not yet Historic.
    /// </summary>
    EditRegistry = 10,

    /// <summary>Edit the rules of the Tenant's Regional competitions: a Tenant Root.</summary>
    EditTenantRules = 11,

    /// <summary>Edit the Tenant's branding: a Tenant Root.</summary>
    EditTenantBranding = 12,

    /// <summary>Edit an Event's branding override: the Main Operator.</summary>
    EditEventBranding = 13,

    /// <summary>Reset a started Event: the Main Operator, while it is Live.</summary>
    ResetEvent = 14,

    /// <summary>Delete an Event that has not started: a Tenant Root of its Tenant.</summary>
    DeleteEvent = 15,

    /// <summary>Search the accounts of a Tenant by name: a Tenant Root, the Main Operator.</summary>
    SearchAccounts = 16,

    /// <summary>Search accounts across Tenants, which the caller has to ask for explicitly: the same people.</summary>
    SearchAccountsAcrossTenants = 17,

    /// <summary>Seed a Tenant Root: the Developer, by command and never through a route.</summary>
    SeedTenantRoot = 18,

    /// <summary>Grant Developer: the Developer, by command and never through a route.</summary>
    GrantDeveloper = 19,
}
