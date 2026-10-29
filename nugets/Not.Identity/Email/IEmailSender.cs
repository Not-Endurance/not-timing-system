namespace Not.Identity.Email;

/// <summary>
/// The seam every email goes through (ADR-0002), so switching the provider is configuration plus one class.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
