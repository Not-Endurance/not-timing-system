using NC.Application.CRUD.Ports;
using NC.Injection;
using NC.Krud.Abstractions;
using NC.Krud.Services;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Judge.Features.Setup.ConfigureEvents.Operators;

public class OperatorService : KrudServiceBase<Operator, OperatorFormModel>, ITransient
{
    public OperatorService(IRepository<Operator> operators, IEnumerable<IKrudMirrorService<Operator>> dependants)
        : base(operators, dependants) { }
}
