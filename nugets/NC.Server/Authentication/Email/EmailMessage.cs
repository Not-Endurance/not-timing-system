namespace NC.Server.Authentication.Email;

internal sealed class EmailMessage
{
    public string To { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string TextBody { get; init; } = string.Empty;
}
