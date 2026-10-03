namespace Not.Identity.Email;

/// <summary>
/// The seam every email goes through (ADR-0002), so switching the provider is configuration plus one class.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// A sender that prints or keeps the mail it is given, and so exposes the one-time codes in it. Production refuses to
/// start with one (<see cref="EmailSenderGuard"/>).
/// </summary>
public interface IDevelopmentEmailSender : IEmailSender { }
