namespace NTS.Domain.Access;

/// <summary>The kinds of place an action is done in: nowhere in particular, a Tenant, or an Event.</summary>
public enum Place
{
    Platform = 0,
    Tenant = 1,
    Event = 2,
}
