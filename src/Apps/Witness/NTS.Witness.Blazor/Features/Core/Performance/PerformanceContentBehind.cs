using Not.Blazor.Components.Abstractions;
using Not.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Objects;
using NTS.Witness.Blazor.Features.Socket;
using NTS.Witness.Features.Core.Dashboard;

namespace NTS.Witness.Blazor.Features.Core.Performance;

public class PerformanceContentBehind : NStatefulComponent
{
    [Inject]
    protected IPerformanceService PerformanceService { get; set; } = default!;

    [Inject]
    protected BlazorSocketService BlazorSocketService { get; set; } = default!;

    protected List<NotListModel<Person>> People { get; set; } = [];
    protected Person SelectedPerson { get; set; } = default!;
    protected Participation? Participation { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(PerformanceService);
        UpdatePeople();
    }

    protected override void OnBeforeRender()
    {
        UpdatePeople();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await BlazorSocketService.EnsureConnected();
        }
    }

    protected void OnPersonChanged(Person person)
    {
        try
        {
            SelectedPerson = person;
            Participation = PerformanceService.GetParticipation(person);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    void UpdatePeople()
    {
        try
        {
            var people = PerformanceService.GetPeople();
            People = NotListModel.FromEntity(people).ToList();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
