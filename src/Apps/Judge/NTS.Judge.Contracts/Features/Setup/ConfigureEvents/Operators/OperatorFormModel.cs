using Not.Krud.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Judge.Contracts.Features.Setup.ConfigureEvents.Operators;

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
