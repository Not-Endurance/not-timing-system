using Not.Application.Behinds.Adapters;
using NTS.Contracts.Branding;
using NTS.Contracts.Core;

namespace NTS.Contracts.Features.Console;

/// <summary>
/// The logos at the head of what an Event prints (#646, ADR-0012): the ones the pages print with, which are what the Api says an
/// Event is printed with (its own logo for a position, and the Tenant's where it has none), and the editing of the Tenant's and
/// of an Event's. A logo is an image of a kind a browser prints and of at most <see cref="LogoLimits.MAX_BYTES"/>, which the Console
/// checks before it sends it and the Api checks again. What the Api refuses is thrown as a domain error, in its words.
/// </summary>
public interface IConsoleBranding : IStatefulService
{
    /// <summary>The logo for the left of what the Event the view shows is printed with, as an address a page can print; none when it has none.</summary>
    string? LeftLogo { get; }

    /// <summary>The logo for the right of what the Event the view shows is printed with, as an address a page can print; none when it has none.</summary>
    string? RightLogo { get; }

    /// <summary>Reads what the Event the view shows is printed with.</summary>
    Task Show(IViewedEvent view);

    /// <summary>The logos of the Tenant the person acts in.</summary>
    Task<Logos> OfTenant();

    /// <summary>What the Event is printed with, each logo with whose it is.</summary>
    Task<Logos> OfEvent(Guid eventId);

    Task SetOfTenant(LogoPosition position, string contentType, byte[] image);

    Task ClearOfTenant(LogoPosition position);

    Task SetOfEvent(Guid eventId, LogoPosition position, string contentType, byte[] image);

    /// <summary>Takes the logo the Event has of its own away, which gives back the Tenant's.</summary>
    Task ClearOfEvent(Guid eventId, LogoPosition position);
}
