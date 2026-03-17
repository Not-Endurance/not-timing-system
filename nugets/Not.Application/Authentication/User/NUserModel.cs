using Not.Objects;
using Not.Random;
using Not.Structures;

namespace Not.Application.Authentication.User;

public class NUserModel : IIdentifiable, IEquatable<NUserModel>
{
    public NUserModel(string email, string[]? roles = null, int? id = null)
    {
        Id = id ?? RandomHelper.GenerateUniqueInteger();
        Roles = roles ?? [];
        Email = email;
    }

    public int Id { get; }
    public string Email { get; }
    public string[] Roles { get; } = [];
    public string? Name { get; set; }
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string? CountryRegion { get; set; }

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
        return ObjectHelper.AreEqual(this, other);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}
