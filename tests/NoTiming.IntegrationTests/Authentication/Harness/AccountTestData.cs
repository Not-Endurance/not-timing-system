using System.Text.Json;

namespace NoTiming.IntegrationTests.Authentication.Harness;

internal static class AccountTestData
{
    public static readonly TimeSpan CodeTimeout = TimeSpan.FromSeconds(15);

    /// <summary>A lower-case address unique to one scenario, so scenarios in one host do not share a challenge.</summary>
    public static string Address(string purpose)
    {
        return $"{purpose}.{Guid.NewGuid():N}@example.test";
    }

    public static int NewId()
    {
        return Random.Shared.Next(100_000, 900_000);
    }

    public static async Task<bool> IsSignedInAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("signedIn").GetBoolean();
    }

    /// <summary>Registers a new address, reads its code from the outbox and signs in. Returns the session cookie.</summary>
    public static async Task<string> RegisterAndSignInAsync(
        AccountClient client,
        MongoAccess mongo,
        string email,
        string givenName = "Ana",
        string surname = "Ivanova",
        string country = "BG"
    )
    {
        var accepted = await client.RegisterAsync(email, givenName, surname, country);
        if (accepted.StatusCode != System.Net.HttpStatusCode.Accepted)
        {
            throw new InvalidOperationException($"Registration was refused with {(int)accepted.StatusCode}.");
        }

        return await VerifyCodeFromOutboxAsync(client, mongo, email);
    }

    /// <summary>Asks for a sign-in code for an existing address, reads it from the outbox and signs in.</summary>
    public static async Task<string> SignInWithCodeAsync(AccountClient client, MongoAccess mongo, string email)
    {
        var accepted = await client.RequestSignInCodeAsync(email);
        if (accepted.StatusCode != System.Net.HttpStatusCode.Accepted)
        {
            throw new InvalidOperationException($"The sign-in code was refused with {(int)accepted.StatusCode}.");
        }

        return await VerifyCodeFromOutboxAsync(client, mongo, email);
    }

    static async Task<string> VerifyCodeFromOutboxAsync(AccountClient client, MongoAccess mongo, string email)
    {
        var code = await mongo.WaitForCodeAsync(email, CodeTimeout);
        var verified = await client.VerifyAsync(email, code);
        if (verified.StatusCode != System.Net.HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"The code was refused with {(int)verified.StatusCode}.");
        }

        return AccountClient.SessionCookieOf(verified);
    }
}
