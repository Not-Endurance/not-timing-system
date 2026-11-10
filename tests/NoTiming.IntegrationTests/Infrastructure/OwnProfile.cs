using NoTiming.Application.Contracts.Account;

namespace NoTiming.IntegrationTests.Infrastructure;

/// <summary>The signed-in person as the profile route returns them: the identity, and NoTiming's profile fields.</summary>
public sealed class OwnProfile
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? DisplayName { get; set; }

    public string? GivenName { get; set; }

    public string? MiddleName { get; set; }

    public string? Surname { get; set; }

    public string[] Roles { get; set; } = [];

    public NoTimingProfile Profile { get; set; } = new();
}
