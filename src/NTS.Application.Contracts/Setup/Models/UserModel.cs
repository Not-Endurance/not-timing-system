using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;


namespace NTS.Application.Contracts.Setup.Models;

public class UserModel : IDocument, IKrudModel<User>
{
    public static UserModel From(User user)
    {
        var model = new UserModel();
        model.MapFrom(user);
        return model;
    }

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public string Email { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string[] Roles { get; set; } = [];

    public void MapFrom(User user)
    {
        Id = user.Id;
        Email = user.Email;
        Name = user.Name;
        Roles = user.Roles.ToArray();
    }

    public User MapToEntity()
    {
        return new User(Email, Name, Roles, Id);
    }
}


