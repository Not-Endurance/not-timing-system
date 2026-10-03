using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Not.Identity.Sessions;

/// <summary>
/// The server side of the session (ADR-0002). The cookie carries only the key of a ticket; the ticket is a document in
/// Mongo, looked up on every request, so a session ends the moment its document is deleted: by signing out, by
/// <see cref="RevokeAllAsync"/> when the security stamp changes, or by the TTL index once it has expired.
/// </summary>
public sealed class MongoTicketStore : ITicketStore, ISessionRevoker
{
    readonly IMongoCollection<SessionDocument> _sessions;
    readonly NIdentityOptions _options;
    readonly TimeProvider _time;

    public MongoTicketStore(IMongoClient client, IOptions<NIdentityOptions> options, TimeProvider time)
    {
        _options = options.Value;
        _sessions = client.GetDatabase(_options.Database).GetCollection<SessionDocument>(_options.SessionsCollection);
        _time = time;
    }

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = NewKey();
        var now = _time.GetUtcNow();
        await _sessions.InsertOneAsync(
            new SessionDocument
            {
                Id = key,
                UserId = UserIdOf(ticket),
                Ticket = TicketSerializer.Default.Serialize(ticket),
                CreatedAt = now.UtcDateTime,
                ExpiresAt = ExpiryOf(ticket, now),
            }
        );
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var now = _time.GetUtcNow();

        // No upsert: a session that was revoked after this request read it must stay gone.
        await _sessions.UpdateOneAsync(
            x => x.Id == key,
            Builders<SessionDocument>
                .Update.Set(x => x.Ticket, TicketSerializer.Default.Serialize(ticket))
                .Set(x => x.ExpiresAt, ExpiryOf(ticket, now))
        );
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var now = _time.GetUtcNow().UtcDateTime;

        // The TTL index removes a document up to a minute after it expires: the expiry is checked here as well.
        var session = await _sessions.Find(x => x.Id == key && x.ExpiresAt > now).FirstOrDefaultAsync();
        return session is null ? null : TicketSerializer.Default.Deserialize(session.Ticket);
    }

    public async Task RemoveAsync(string key)
    {
        await _sessions.DeleteOneAsync(x => x.Id == key);
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _sessions.DeleteManyAsync(x => x.UserId == userId, cancellationToken);
    }

    DateTime ExpiryOf(AuthenticationTicket ticket, DateTimeOffset now)
    {
        return (ticket.Properties.ExpiresUtc ?? now + _options.SessionLifetime).UtcDateTime;
    }

    static Guid UserIdOf(AuthenticationTicket ticket)
    {
        return Guid.TryParse(ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : throw new InvalidOperationException("A session needs a user: the ticket has no user id claim.");
    }

    /// <summary>256 bits from the system generator: the key is a secret, not an identifier.</summary>
    static string NewKey()
    {
        return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
    }
}
