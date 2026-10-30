using Not.Blazor.Components.Abstractions;
using NTS.Contracts.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;

namespace NoTiming.Ui.Components.EventViews;

/// <summary>
/// The Rankings of the viewed Event and the Results of the one the person picked (#630, ADR-0007). The Results are composed
/// from the Participations the view holds whenever they are shown (ADR-0006), so the same view shows a Live Event as it is
/// now and a Historic Event as a record.
/// </summary>
public class EventResultsBehind : NStatefulComponent
{
    ResultsDocument? _document;
    Ranking? _selected;

    [CascadingParameter]
    IViewedEvent View { get; set; } = default!;

    protected bool IsEmpty => _document == null;
    protected ResultsDocument? Document => _document;
    protected IReadOnlyList<Ranking> Rankings => View.Rankings;
    protected Ranking? CurrentRanking => _selected;

    protected override async Task OnInitializedAsync()
    {
        _selected = View.Rankings.FirstOrDefault();
        Compose();
        await Observe(View);
    }

    protected override void OnBeforeRender()
    {
        Compose();
    }

    protected void SelectRanking(Ranking ranking)
    {
        try
        {
            _selected = Rankings.FirstOrDefault(x => x.Id == ranking.Id) ?? ranking;
            Compose();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    void Compose()
    {
        _document = _selected == null ? null : View.CreateDocument(_selected);
    }
}
