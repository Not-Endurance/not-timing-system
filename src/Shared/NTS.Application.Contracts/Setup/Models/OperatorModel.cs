using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Application.Contracts.Setup.Models;

public class OperatorModel
{
    public static OperatorModel MapFrom(Operator @operator)
    {
        return new OperatorModel
        {
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
        return new Operator(User.MapToEntity(), Id, Role);
    }
}
