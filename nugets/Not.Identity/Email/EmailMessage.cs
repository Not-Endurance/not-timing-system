namespace Not.Identity.Email;

public sealed record EmailMessage
{
    /// <param name="to">The recipient's address.</param>
    /// <param name="subject">The subject, already in the recipient's language.</param>
    /// <param name="textBody">The plain-text body, already in the recipient's language.</param>
    /// <param name="htmlBody">The same message as HTML, when the provider takes both.</param>
    /// <param name="language">The language the message is written in, such as <c>bg</c>.</param>
    public EmailMessage(string to, string subject, string textBody, string? htmlBody = null, string? language = null)
    {
        To = to;
        Subject = subject;
        TextBody = textBody;
        HtmlBody = htmlBody;
        Language = language;
    }

    public string To { get; }
    public string Subject { get; }
    public string TextBody { get; }
    public string? HtmlBody { get; }
    public string? Language { get; }
}
