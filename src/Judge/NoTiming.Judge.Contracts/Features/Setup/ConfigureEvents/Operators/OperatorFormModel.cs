using NC.Krud.Models;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Setup.Aggregates;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Judge.Contracts.Features.Setup.ConfigureEvents.Operators;

public record OperatorFormModel : KrudFormModel<Operator>
{
    public User? User { get; set; }
    public OfficialRole Role { get; private set; } = OfficialRole.Steward;

    protected override Operator MapTo()
    {
        return new Operator(User, Id, Role);
    }

    public override void MapFrom(Operator @operator)
    {
        Id = @operator.Id;
        User = @operator.User;
        Role = @operator.Role;
    }
}
