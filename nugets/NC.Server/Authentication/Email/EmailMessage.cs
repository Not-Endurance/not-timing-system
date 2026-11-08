namespace NC.Server.Authentication.Email;

public sealed class EmailMessage
{
    public string To { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string TextBody { get; init; } = string.Empty;
}
