namespace NC.Server.Authentication.Email;

/// <summary>The one seam through which mail leaves the application. Changing provider is one class and configuration.</summary>
internal interface IEmailSender
{
    Task Send(EmailMessage message, CancellationToken cancellationToken);
}
