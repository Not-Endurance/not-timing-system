using Microsoft.AspNetCore.Components;
using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Formatting;
using NTS.Application.Contracts.Presentlists;
using NTS.Domain.Core.Objects.Presentlists;
using NTS.Domain.Helpers;

namespace NTS.Blazor.Components.Presentlist;

public class PresentlistTableBehind : NStatefulComponent
{
    static readonly TimeSpan WARNING_THRESHOLD = TimeSpan.FromMinutes(2);

    [Inject]
    protected IPresentlistService Service { get; set; } = default!;

    [Inject]
    protected IEnumerable<IPresentlistAccess> AccessPolicies { get; set; } = [];

    [Inject]
    protected IDialogService DialogService { get; set; } = default!;

    protected bool CanAcknowledge => Service.CanAcknowledge && AccessPolicies.Any(x => x.CanAcknowledgePresentations);

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
        foreach (var access in AccessPolicies)
        {
            await Observe(access);
        }
    }

    protected string FormatAthlete(PresentlistEntry entry)
    {
        return NameRenderingHelper.Render(entry.AthleteName, entry.AthleteNameEnglish, entry.Ruleset);
    }

    protected string FormatTime(PresentlistEntry entry)
    {
        var time = entry.Time.ToDateTimeOffset();
        var delta = time - DateTimeOffset.Now;
        return delta <= TimeSpan.Zero ? entry.Time.ToString() : FormattingHelper.Format(delta);
    }

    protected Color GetTimeColor(PresentlistEntry entry)
    {
        var delta = entry.Time.ToDateTimeOffset() - DateTimeOffset.Now;
        if (delta <= WARNING_THRESHOLD)
        {
            return Color.Error;
        }

        return entry.Type is PresentlistEntryType.Present or PresentlistEntryType.Represent
            ? Color.Warning
            : Color.Default;
    }

    protected string FormatType(PresentlistEntryType type)
    {
        return type switch
        {
            PresentlistEntryType.Present => Presentation_string,
            PresentlistEntryType.Represent => Represent_string,
            PresentlistEntryType.RI => RI_string,
            PresentlistEntryType.CRI => CRI_string,
            _ => type.ToString(),
        };
    }

    protected async Task Acknowledge(PresentlistEntry entry, bool value)
    {
        if (!value)
        {
            return;
        }

        try
        {
            var confirmed = await DialogService.ShowMessageBox(
                Confirm_action_string,
                Is_on_time_for_Presentation_Inspection_string,
                yesText: Yes_string,
                cancelText: Cancel_string
            );
            if (confirmed == true)
            {
                await Service.Acknowledge(entry);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected void Tick()
    {
        Service.Tick();
    }
}
