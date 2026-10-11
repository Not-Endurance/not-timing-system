using Microsoft.AspNetCore.Components.Forms;
using Not.Blazor.Components.Abstractions;
using Not.Domain.Exceptions;
using Not.Notify;
using NTS.Contracts.Branding;
using NTS.Contracts.Features.Console;

namespace NoTiming.Console.Features.Branding;

/// <summary>
/// The logos of a Tenant, or of an Event (#646, ADR-0012), with what puts a file in the place of each and what takes it away. For
/// an Event they are what it is printed with, so a position that has none of its own shows the Tenant's, and taking its own away
/// gives that back. A file is read whole and sent: its kind and its weight are checked here, so that a person is told before the
/// wait, and by the Api, which does not take a file for what it says it is.
/// </summary>
public class BrandingPanelBehind : NStatefulComponent
{
    [Inject]
    IConsoleBranding Branding { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    protected Logos Current { get; private set; } = Logos.None;
    protected string RemoveText => EventId == null ? Remove_logo_string : Use_the_federations_logo_string;
    protected string Hint => string.Format(Logos_hint_string, LogoLimits.MAX_BYTES / 1024);

    /// <summary>The Event whose logos these are; none for the Tenant's.</summary>
    [Parameter]
    public Guid? EventId { get; set; }

    [Parameter]
    public bool CanEdit { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await Read();
    }

    protected async Task Choose(LogoPosition position, IBrowserFile file)
    {
        await Safely(async () =>
        {
            if (file.Size > LogoLimits.MAX_BYTES)
            {
                throw new DomainException(string.Format(Logo_is_too_large_string, LogoLimits.MAX_BYTES / 1024));
            }

            await using var stream = file.OpenReadStream(LogoLimits.MAX_BYTES);
            using var image = new MemoryStream();
            await stream.CopyToAsync(image);
            if (EventId is { } eventId)
            {
                await Branding.SetOfEvent(eventId, position, file.ContentType, image.ToArray());
            }
            else
            {
                await Branding.SetOfTenant(position, file.ContentType, image.ToArray());
            }

            Notifier.Success(Logo_saved_string);
        });
    }

    protected async Task Remove(LogoPosition position)
    {
        await Safely(async () =>
        {
            if (EventId is { } eventId)
            {
                await Branding.ClearOfEvent(eventId, position);
            }
            else
            {
                await Branding.ClearOfTenant(position);
            }

            Notifier.Success(Logo_removed_string);
        });
    }

    async Task Read()
    {
        await Safely(() => Task.CompletedTask); // which reads, whether the action was one or none
    }

    /// <summary>What the Api or the check refuses is said, in its own words, to the person; anything else is an error of the app.</summary>
    async Task Safely(Func<Task> action)
    {
        try
        {
            await action();
            Current = EventId is { } eventId ? await Branding.OfEvent(eventId) : await Branding.OfTenant();
        }
        catch (DomainException ex)
        {
            Notifier.Warn(ex.Message);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
