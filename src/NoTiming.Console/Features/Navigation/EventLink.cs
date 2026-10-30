namespace NoTiming.Console.Features.Navigation;

/// <summary>A page of an Event that the person may go to from the page they are on (#646): what it is called, where it is, and whether it is the one they are on.</summary>
public sealed class EventLink
{
    public EventLink(string text, string route, bool isCurrent)
    {
        Text = text;
        Route = route;
        IsCurrent = isCurrent;
    }

    public string Text { get; }
    public string Route { get; }
    public bool IsCurrent { get; }
}
