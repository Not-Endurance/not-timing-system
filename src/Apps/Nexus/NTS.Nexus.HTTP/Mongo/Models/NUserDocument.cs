using Not.Application.Authentication.User;
using Not.Random;
using NTS.Application.Shared;

namespace NTS.Nexus.HTTP.Mongo.Models;

public class NUserDocument : IDocument
{
    public static NUserDocument Create(string email)
    {
        return new NUserDocument
        {
            Id = RandomHelper.GenerateUniqueInteger(),
            Email = email,
        };
    }

    public static NUserDocument From(NUserModel user)
    {
        return new NUserDocument
        {
            Id = user.Id == default ? RandomHelper.GenerateUniqueInteger() : user.Id,
            Email = user.Email,
            Name = user.Name,
            Roles = user.Roles.ToArray(),
        };
    }

    public int Id { get; set; }
    public string Email { get; set; } = default!;
    public string? Name { get; set; }
    public string[] Roles { get; set; } = [];
    public string TenantId { get; set; } = "nts";

    public NUserModel ToUser()
    {
        return new NUserModel(Email, Roles, Id) { Name = Name };
    }
}
