using NC.Application.Authentication.User;
using NoTiming.Application.Contracts.Shared;
using NoTiming.Application.Contracts.Shared.Models;

namespace NoTiming.Api.Application.Mongo.Models;

public class NUserDocument : IDocument
{
    public int Id { get; set; }
    public string Email { get; set; } = default!;
    public string? Name { get; set; }
    public string? DisplayName { get; set; }
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string? CountryRegion { get; set; }
    public string? MiddleName { get; set; }
    public string? Club { get; set; }
    public string? FeiId { get; set; }
    public string[] Roles { get; set; } = [];
    public string TenantId { get; set; } = "nts";

    public NUserModel ToUser()
    {
        return new NUserModel(Email, Roles, Id)
        {
            Name = Name,
            DisplayName = DisplayName,
            GivenName = GivenName,
            Surname = Surname,
            CountryRegion = CountryRegion,
            MiddleName = MiddleName,
            Club = Club,
            FeiId = FeiId,
        };
    }
}
