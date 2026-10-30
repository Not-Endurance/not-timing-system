namespace NTS.Contracts.Branding;

/// <summary>
/// What a logo may be (#646, ADR-0012): the one place the Api that keeps it and the Console that sends it have it, so that a
/// person is told what the server would say before they wait for it. The kinds are the images a browser prints, by their media
/// type, and a logo weighs at most 512 KiB, as the image and not as the text it travels as.
/// </summary>
public static class LogoLimits
{
    public const int MAX_BYTES = 512 * 1024;
    public static readonly IReadOnlyList<string> CONTENT_TYPES = ["image/png", "image/jpeg", "image/gif", "image/webp"];
}
