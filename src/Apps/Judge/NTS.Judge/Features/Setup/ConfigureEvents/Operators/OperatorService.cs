using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Services;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Judge.Features.Setup.ConfigureEvents.Operators;

public class OperatorService : KrudServiceBase<Operator, OperatorFormModel>, ITransient
{
    public OperatorService(IRepository<Operator> operators, IEnumerable<IKrudMirrorService<Operator>> dependants)
        : base(operators, dependants) { }
}
