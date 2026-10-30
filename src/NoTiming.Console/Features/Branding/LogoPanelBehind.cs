using Microsoft.AspNetCore.Components.Forms;
using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Branding;

namespace NoTiming.Console.Features.Branding;

/// <summary>
/// One of the two places on the head of a document (#646): the logo it has, whose it is when it is the one an Event is printed
/// with, the file input that puts another in its place and what takes it away. It decides nothing: the panel that holds it
/// sends the file to the Api, which checks it again.
/// </summary>
public class LogoPanelBehind : NComponent
{
    protected static string Accept { get; } = string.Join(',', LogoLimits.CONTENT_TYPES);

    protected string? SourceText =>
        Logo?.Source switch
        {
            LogoSource.Event => Logo_of_the_event_string,
            LogoSource.Tenant => Logo_of_the_federation_string,
            _ => null,
        };

    /// <summary>A logo that is the Tenant's, in an Event, is not the Event's to take away: it is the Event's own that is, to give back the Tenant's.</summary>
    protected bool CanRemove => Logo != null && Logo.Source != LogoSource.Tenant;

    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public string TestId { get; set; } = string.Empty;

    [Parameter]
    public Logo? Logo { get; set; }

    [Parameter]
    public bool CanEdit { get; set; } = true;

    [Parameter]
    public string RemoveText { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<IBrowserFile> OnChosen { get; set; }

    [Parameter]
    public EventCallback OnRemoved { get; set; }

    protected async Task Choose(InputFileChangeEventArgs args)
    {
        try
        {
            await OnChosen.InvokeAsync(args.File);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task Remove()
    {
        try
        {
            await OnRemoved.InvokeAsync();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
