using NoTiming.Application.Contracts.Shared.Models;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;
using NoTiming.Application.Contracts.Shared;

namespace NoTiming.Application.Contracts.Setup.Models;

public class OperatorModel : PersistedModel
{
    public static OperatorModel MapFrom(Operator @operator)
    {
        return new OperatorModel
        {
            DomainVersion = @operator.DomainVersion,
            Id = @operator.Id,
            User = UserModel.From(@operator.User),
            Role = @operator.Role,
        };
    }

    public int Id { get; init; }
    public UserModel User { get; init; } = default!;
    public OfficialRole Role { get; init; } = OfficialRole.Steward;

    public Operator MapToEntity()
    {
        return new Operator(User.MapToEntity(), Id, Role, storedDomainVersion: DomainVersion);
    }
}
