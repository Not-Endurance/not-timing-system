namespace NC.Server.Tests.Authentication.Fakes;

/// <summary>A profile the way an app defines one: a parameterless constructor and public properties.</summary>
public sealed class TestProfile
{
    public string? Club { get; set; }

    public string? FeiId { get; set; }

    public string? CountryIsoCode { get; set; }
}
