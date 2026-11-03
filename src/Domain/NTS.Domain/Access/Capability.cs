namespace NTS.Domain.Access;

/// <summary>The rows of the access matrix of ADR-0012. The policy answers for one of them at a time, for who stands at a place.</summary>
public enum Capability
{
    /// <summary>Read the public views of an Event: anybody, signed in or not.</summary>
    ReadPublicViews = 1,

    /// <summary>
    /// What an account does for itself or reads of the registries (its profile, state and passkeys, its link to a rider, the
    /// history of a Participant, the registry of its Tenant, the search by name): any signed-in account.
    /// </summary>
    SignedIn = 2,

    /// <summary>See how times were recorded: Staff.</summary>
    SeeTimeEvents = 3,

    /// <summary>Send a Snapshot: Staff whose grant may record, while the Event is Live.</summary>
    SendSnapshot = 4,

    /// <summary>Configure an Event: start it, change it, brand it, link Operators and Officials.</summary>
    ConfigureEvent = 5,

    /// <summary>Edit the Setup, which is what the Event is configured with until it starts.</summary>
    EditSetup = 6,

    /// <summary>Read the Setup and who has access.</summary>
    ReadConfiguration = 7,

    /// <summary>Run the Event: edit what it keeps and reset it, while it is Live.</summary>
    RunEvent = 8,

    /// <summary>Assign and remove Console Operators: Root.</summary>
    AssignConsoleOperators = 9,

    /// <summary>Delete an Event that has not started: Root.</summary>
    DeleteEvent = 10,

    /// <summary>Create an Event in a Tenant: Root.</summary>
    CreateEvent = 11,

    /// <summary>Edit the rules and the logos of a Tenant: Root.</summary>
    EditTenant = 12,

    /// <summary>Edit the Tenant's registry and search its accounts by name: a Console Operator of an Event of it, and Root.</summary>
    EditRegistry = 13,

    /// <summary>
    /// Read the whole row of a rider of another Tenant, match a search against its FEI ID and search accounts across Tenants:
    /// the people who set an Event up.
    /// </summary>
    SetUpAcrossTenants = 14,

    /// <summary>Edit the countries, the platform's reference data: the Developer.</summary>
    EditCountries = 15,
}
