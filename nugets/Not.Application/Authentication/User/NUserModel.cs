using Not.Structures;

namespace Not.Application.Authentication.User;

public class NUserModel : IIdentifiable, IEquatable<NUserModel>
{
    string? _name;

    public NUserModel(string email, string[]? roles = null, Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        Roles = roles ?? [];
        Email = email;
    }

    public Guid Id { get; }
    public string Email { get; }
    public string[] Roles { get; } = [];
    public string? Name
    {
        get => BuildName(GivenName, MiddleName, Surname) ?? _name;
        set => _name = Normalize(value);
    }
    public string? DisplayName { get; set; }
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string? CountryRegion { get; set; }
    public string? MiddleName { get; set; }
    public string? Club { get; set; }
    public string? FeiId { get; set; }

    public override bool Equals(object? other)
    {
        if (other is not NUserModel user)
        {
            return false;
        }
        return Equals(user);
    }

    public bool Equals(NUserModel? other)
    {
        return other is not null && other.GetType() == GetType() && other.Id == Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    static string? BuildName(params string?[] parts)
    {
        var nameParts = parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()).ToArray();
        return nameParts.Length == 0 ? null : string.Join(" ", nameParts);
    }

    static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
