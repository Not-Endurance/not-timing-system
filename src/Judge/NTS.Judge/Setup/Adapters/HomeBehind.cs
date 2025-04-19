using Not.Application.Behinds;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.EnduranceEvents;
using NTS.Judge.Core.Behinds;

namespace NTS.Judge.Setup.Adapters;

public class HomeBehind : ObservableBehind, IEnduranceEventBehind
{
    readonly IRepository<EnduranceEvent> _events;
    readonly EnduranceEventCrudeContext _crudeContext;
    readonly ICrudParent<Competition> _competitionParent;
    readonly ICrudParent<Official> _officialParent;

    public HomeBehind(IRepository<EnduranceEvent> events, EnduranceEventCrudeContext crudeContext)
    {
        _events = events;
        _crudeContext = crudeContext;
        _competitionParent = crudeContext;
        _officialParent = crudeContext;
    }

    public EnduranceEventFormModel? Model { get; private set; }

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> _)
    {
        var enduranceEvent = await _events.Read(0);
        if (enduranceEvent == null)
        {
            return false;
        }
        _crudeContext.SetParent(enduranceEvent);
        Model = new EnduranceEventFormModel();
        Model.FromEntity(enduranceEvent);
        return false;
    }

    public async Task Create(EnduranceEventFormModel model)
    {
        var enduranceEvent = EnduranceEvent.Create(model.Place, model.Country, model.FeiShowId);
        await _events.Create(enduranceEvent);
        _crudeContext.SetParent(enduranceEvent);
        Model = new EnduranceEventFormModel();
        Model.FromEntity(enduranceEvent);
        EmitChange();
    }

    public async Task Update(EnduranceEventFormModel model)
    {
        var enduranceEvent = EnduranceEvent.Update(
            model.Id,
            model.Place,
            model.Country,
            model.FeiShowId,
            _competitionParent.Children,
            _officialParent.Children
        );
        await _events.Update(enduranceEvent);
        _crudeContext.SetParent(enduranceEvent);
        EmitChange();
    }

    public Task<EnduranceEvent> Delete(EnduranceEvent enduranceEvent)
    {
        throw new NotImplementedException("Endurance event cannot be deleted");
    }
}
