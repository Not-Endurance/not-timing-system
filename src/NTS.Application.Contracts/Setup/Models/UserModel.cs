using Not.Krud.Abstractions;
using Newtonsoft.Json;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Application.Contracts.Setup.Models;

public class UserModel : IDocument, IKrudModel<User>
{
    public static UserModel From(User user)
    {
        var model = new UserModel();
        model.MapFrom(user);
        return model;
    }

    string? _name;

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public string Email { get; set; } = default!;
    public string Name
    {
        get => BuildName(GivenName, MiddleName, Surname) ?? _name ?? Email;
        set => _name = Normalize(value);
    }
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? DisplayName { get; set; }
    public string? GivenName { get; set; }
    public string? MiddleName { get; set; }
    public string? Surname { get; set; }
    public string? CountryRegion { get; set; }
    public string? Club { get; set; }
    public string? FeiId { get; set; }
    public string[] Roles { get; set; } = [];

    public void MapFrom(User user)
    {
        Id = user.Id;
        Email = user.Email;
        Name = user.Name;
        DisplayName = user.DisplayName;
        GivenName = user.GivenName;
        MiddleName = user.MiddleName;
        Surname = user.Surname;
        CountryRegion = user.CountryRegion;
        Club = user.Club;
        FeiId = user.FeiId;
        Roles = user.Roles.ToArray();
    }

    public User MapToEntity()
    {
        return new User(Email, Name, Roles, Id, GivenName, MiddleName, Surname, CountryRegion, Club, FeiId, DisplayName);
    }

    static string? BuildName(params string?[] parts)
    {
        var nameParts = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
        return nameParts.Length == 0 ? null : string.Join(" ", nameParts);
    }

    static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
