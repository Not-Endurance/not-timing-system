using NoTiming.Application.Contracts.Shared;
using NoTiming.Application.Contracts.Shared.Models;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Application.Contracts.Setup.Models;

public class OperatorModel
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

    public int DomainVersion { get; set; }

    public int Id { get; init; }
    public UserModel User { get; init; } = default!;
    public OfficialRole Role { get; init; } = OfficialRole.Steward;

    public Operator MapToEntity()
    {
        return new Operator(User.MapToEntity(), Id, Role, storedDomainVersion: DomainVersion);
    }
}
