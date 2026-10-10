using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Domain.Exceptions;
using Not.Extensions;
using Not.Notify;
using NTS.Domain.Access;

namespace NoTiming.Console.Features.Setup.Access;

/// <summary>
/// The people linked to the Officials and Operators of an Event (#646, ADR-0012): who they are as the Api tells it (an
/// address is partly masked and never given whole), whether the invitation still waits for the person to register, and a
/// way to link one more by the exact email or to end the access of one.
/// </summary>
public class AccessPanelBehind : NComponent
{
    [Inject]
    IConsoleGrants Grants { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    INotifier Notifier { get; set; } = default!;

    protected IReadOnlyList<GrantRow> Rows { get; private set; } = [];
    protected bool IsLoading { get; private set; } = true;

    [Parameter, EditorRequired]
    public Guid EventId { get; set; }

    [Parameter]
    public bool ReadOnly { get; set; }

    protected static string KindText(GrantRow row)
    {
        return row.Kind == GrantKind.Operator ? Operator_string : (row.Role?.GetDescription() ?? Official_string);
    }

    protected static string PersonText(GrantRow row)
    {
        return string.IsNullOrWhiteSpace(row.DisplayName) ? row.Email : $"{row.DisplayName} ({row.Email})";
    }

    protected override async Task OnParametersSetAsync()
    {
        await Load();
    }

    protected async Task Link()
    {
        try
        {
            var parameters = new DialogParameters<LinkAccountDialog> { { x => x.EventId, EventId } };
            var dialog = await DialogService.ShowAsync<LinkAccountDialog>(Link_person_string, parameters);
            var result = await dialog.Result;
            if (result is not { Canceled: false })
            {
                return;
            }

            Notifier.Success(Person_linked_string);
            await Load();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task Remove(GrantRow row)
    {
        try
        {
            await Grants.Remove(row.Id);
            Notifier.Success(Access_removed_string);
            await Load();
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

    async Task Load()
    {
        try
        {
            Rows = await Grants.List(EventId);
        }
        catch (DomainException ex)
        {
            Notifier.Warn(ex.Message);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
        finally
        {
            IsLoading = false;
        }

        await InvokeRender();
    }
}
