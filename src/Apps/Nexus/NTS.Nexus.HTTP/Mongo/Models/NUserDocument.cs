using Not.Application.Authentication.User;
using Not.Random;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;

namespace NTS.Nexus.HTTP.Mongo.Models;

public class NUserDocument : IDocument
{
    public static NUserDocument Create(
        string email,
        string? name = null,
        string? givenName = null,
        string? surname = null,
        string? countryRegion = null
    )
    {
        return new NUserDocument
        {
            Id = RandomHelper.GenerateUniqueInteger(),
            Email = email,
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            GivenName = string.IsNullOrWhiteSpace(givenName) ? null : givenName.Trim(),
            Surname = string.IsNullOrWhiteSpace(surname) ? null : surname.Trim(),
            CountryRegion = string.IsNullOrWhiteSpace(countryRegion) ? null : countryRegion.Trim(),
        };
    }

    public static NUserDocument From(NUserModel user)
    {
        return new NUserDocument
        {
            Id = user.Id == default ? RandomHelper.GenerateUniqueInteger() : user.Id,
            Email = user.Email,
            Name = user.Name,
            GivenName = user.GivenName,
            Surname = user.Surname,
            CountryRegion = user.CountryRegion,
            Roles = user.Roles.ToArray(),
        };
    }

    public int Id { get; set; }
    public string Email { get; set; } = default!;
    public string? Name { get; set; }
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string? CountryRegion { get; set; }
    public string[] Roles { get; set; } = [];
    public string TenantId { get; set; } = "nts";

    public NUserModel ToUser()
    {
        return new NUserModel(Email, Roles, Id)
        {
            Name = Name,
            GivenName = GivenName,
            Surname = Surname,
            CountryRegion = CountryRegion,
        };
    }
}
