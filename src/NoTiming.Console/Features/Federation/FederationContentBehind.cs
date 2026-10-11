using Not.Blazor.Components.Abstractions;
using Not.Domain.Exceptions;
using Not.Notify;
using NTS.Contracts.Features.Console;

namespace NoTiming.Console.Features.Federation;

/// <summary>
/// The federation (the Tenant) the person acts in (#646, ADR-0012): the rules of its Regional competitions, which a Tenant Root edits and
/// an Event copies when it starts. What the person may do is what the Api says of them, and a person who may do nothing here
/// is shown nothing.
/// </summary>
public class FederationContentBehind : NStatefulComponent
{
    bool? _onlyAverageLoopSpeed;
    string? _rankerCode;
    bool _rankerCodeChanged;

    [Inject]
    IConsoleTenant Federation { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    protected string Title => string.IsNullOrWhiteSpace(Federation.Name) ? Federation_string : Federation.Name;
    protected bool MayEdit => Federation.CanEditRules || Federation.CanEditBranding;
    protected bool CanEditRules => Federation.CanEditRules;
    protected bool CanEditBranding => Federation.CanEditBranding;

    protected bool OnlyAverageLoopSpeed
    {
        get => _onlyAverageLoopSpeed ?? Federation.OnlyAverageLoopSpeed;
        set => _onlyAverageLoopSpeed = value;
    }

    protected string? RankerCode
    {
        get => _rankerCodeChanged ? _rankerCode : Federation.RankerCode;
        set
        {
            _rankerCode = value;
            _rankerCodeChanged = true;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await Observe(Federation);
    }

    protected void GoHome()
    {
        Navigator.NavigateTo(ConsoleRoutes.HOME);
    }

    protected async Task Save()
    {
        try
        {
            await Federation.SaveRules(OnlyAverageLoopSpeed, RankerCode);
            _onlyAverageLoopSpeed = null;
            _rankerCodeChanged = false;
            Notifier.Success(Rules_saved_string);
        }
        catch (DomainException ex)
        {
            Notifier.Warn(ex.Message); // what was typed stays, to be corrected
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
