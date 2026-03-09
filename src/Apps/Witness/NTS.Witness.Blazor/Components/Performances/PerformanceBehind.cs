using Not.Blazor.Components.Abstractions;
using Not.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Objects;
using NTS.Witness.Services;

namespace NTS.Witness.Blazor.Components.Performances;

public class PerformanceBehind : NStatefulComponent
{
    protected List<NotListModel<Person>> People { get; set; } = [];
    protected Person SelectedPerson { get; set; } = default!;
    protected Participation? Participation { get; set; } = default!;

    [Inject]
    protected IPerformanceService Service { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
        UpdatePeople();
    }

    protected override void OnBeforeRender()
    {
        UpdatePeople();
    }

    protected void OnPersonChanged(Person person)
    {
        try
        {
            SelectedPerson = person;
            Participation = Service.GetParticipation(person);
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
            var people = Service.GetPeople();
            People = NotListModel.FromEntity(people).ToList();
            //to auto assign competitor based on profile
            //check if competitor profile name matches any person in the people list
            //might be beneficial to have team relations so that we can show their team the performance
            //consider showing temporary Ranking in Performance as well
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
