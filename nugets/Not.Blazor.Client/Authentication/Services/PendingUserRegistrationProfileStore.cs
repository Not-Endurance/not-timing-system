using System.Text.Json;
using Not.Application.Authentication.User;
using Not.BLazor.Client.Browser;

namespace Not.Blazor.Client.Authentication.Services;

internal interface INPendingUserRegistrationProfileStore
{
    Task Write(NUserRegistrationProfile profile);
    Task<NUserRegistrationProfile?> Read();
    Task Clear();
}

internal class PendingUserRegistrationProfileStore : INPendingUserRegistrationProfileStore
{
    internal const string STORAGE_KEY = "not.auth.pending-registration-profile";
    internal static readonly TimeSpan PROFILE_LIFETIME = TimeSpan.FromMinutes(30);

    readonly IBrowserLocalStorage _browserLocalStorage;

    public PendingUserRegistrationProfileStore(IBrowserLocalStorage browserLocalStorage)
    {
        _browserLocalStorage = browserLocalStorage;
    }

    public async Task Write(NUserRegistrationProfile profile)
    {
        var document = PendingUserRegistrationProfileDocument.From(profile, DateTimeOffset.UtcNow);
        await _browserLocalStorage.Write(STORAGE_KEY, JsonSerializer.Serialize(document));
    }

    public async Task<NUserRegistrationProfile?> Read()
    {
        var rawValue = await _browserLocalStorage.Read(STORAGE_KEY);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        PendingUserRegistrationProfileDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<PendingUserRegistrationProfileDocument>(rawValue);
        }
        catch (JsonException)
        {
            await Clear();
            return null;
        }

        if (document == null || document.IsExpired(DateTimeOffset.UtcNow, PROFILE_LIFETIME))
        {
            await Clear();
            return null;
        }

        return document.ToProfile();
    }

    public async Task Clear()
    {
        await _browserLocalStorage.Clear(STORAGE_KEY);
    }

    internal sealed class PendingUserRegistrationProfileDocument
    {
        public PendingUserRegistrationProfileDocument() { }

        public PendingUserRegistrationProfileDocument(
            string? name,
            string? givenName,
            string? middleName,
            string? surname,
            string? club,
            string? feiId,
            string? displayName,
            long createdAtUnixMilliseconds
        )
        {
            Name = name;
            GivenName = givenName;
            MiddleName = middleName;
            Surname = surname;
            Club = club;
            FeiId = feiId;
            DisplayName = displayName;
            CreatedAtUnixMilliseconds = createdAtUnixMilliseconds;
        }

        public string? Name { get; set; }
        public string? DisplayName { get; set; }
        public string? GivenName { get; set; }
        public string? MiddleName { get; set; }
        public string? Surname { get; set; }
        public string? Club { get; set; }
        public string? FeiId { get; set; }
        public long CreatedAtUnixMilliseconds { get; set; }

        internal static PendingUserRegistrationProfileDocument From(
            NUserRegistrationProfile profile,
            DateTimeOffset createdAt
        )
        {
            return new PendingUserRegistrationProfileDocument(
                profile.Name,
                profile.GivenName,
                profile.MiddleName,
                profile.Surname,
                profile.Club,
                profile.FeiId,
                profile.DisplayName,
                createdAt.ToUnixTimeMilliseconds()
            );
        }

        internal bool IsExpired(DateTimeOffset now, TimeSpan lifetime)
        {
            var createdAt = DateTimeOffset.FromUnixTimeMilliseconds(CreatedAtUnixMilliseconds);
            return now - createdAt > lifetime;
        }

        internal NUserRegistrationProfile ToProfile()
        {
            return new NUserRegistrationProfile(Name, GivenName, MiddleName, Surname, Club, FeiId, DisplayName);
        }
    }
}
