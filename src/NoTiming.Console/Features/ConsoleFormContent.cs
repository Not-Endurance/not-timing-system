using Not.Blazor.Components;
using Not.Exceptions;
using Not.Krud.Abstractions;
using Not.Krud.Models;

namespace NoTiming.Console.Features;

/// <summary>
/// A page that edits one row of a list (#646): a Competition of the Setup, an Athlete of the registry. The list that opens it
/// hands it the form; a page that is opened by its address (a reload, a bookmark) has been handed none, and the page it came
/// from is where the person goes, as the page cannot be shown without what it edits.
/// </summary>
public abstract class ConsoleFormContent<T> : NContentBehind
    where T : IKrudFormModel, new()
{
    /// <summary>The page the list is on, which is where the person goes back to.</summary>
    protected abstract string BackRoute { get; }

    [Inject]
    NavigationManager Pages { get; set; } = default!;

    protected T Model { get; set; } = default!;
    protected bool ReadOnly { get; private set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        try
        {
            var handed = GetRouteParameter<KrudFormRouteParameter<T>>();
            Model = handed.Model;
            ReadOnly = handed.ReadOnly;
        }
        catch (GuardException)
        {
            GoBack(replace: true);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected void GoBack(bool replace = false)
    {
        Pages.NavigateTo(BackRoute, replace: replace);
    }
}
