namespace NoTiming.Console.Features.Home.Dialogs;

/// <summary>What the person typed to make an Event: the Api takes these three and nothing else, the country being the Tenant's.</summary>
public sealed class NewEvent
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? FeiShowId { get; set; }
}
