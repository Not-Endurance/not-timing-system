namespace NoTiming.Console;

/// <summary>
/// The addresses of the Console (#646). Every one starts with the segment <c>console</c>, which is what names the assembly
/// that the router fetches before it looks for a page (<c>LazyRoutes</c>): a phone that follows an Event never has it.
/// </summary>
public static class ConsoleRoutes
{
    const string EVENT_ID_SEGMENT = "{eventId:guid}";
    public const string FIRST_SEGMENT = "console";
    public const string HOME = "/console";
    public const string EVENT_SETUP_PAGE = $"/console/events/{EVENT_ID_SEGMENT}/setup";
    public const string EVENT_DASHBOARD_PAGE = $"/console/events/{EVENT_ID_SEGMENT}/dashboard";
}
