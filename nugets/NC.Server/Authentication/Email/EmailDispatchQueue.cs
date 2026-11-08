using System.Threading.Channels;

namespace NC.Server.Authentication.Email;

/// <summary>
/// Queues mail for sending in the background, so a sign-in response takes the same time whether or not an email is sent.
/// </summary>
public sealed class EmailDispatchQueue
{
    readonly Channel<EmailMessage> _messages = Channel.CreateUnbounded<EmailMessage>();

    public ChannelReader<EmailMessage> Reader => _messages.Reader;

    public void Enqueue(EmailMessage message)
    {
        _messages.Writer.TryWrite(message);
    }
}
