using System.Buffers.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// The passkey routes (ADR-0002). The ceremony routes are ported from the reference implementation of Identity in
/// .NET 10 and keep what it requires of them: the options and the credential are posted with an antiforgery token, and
/// the ones that act for a person need that person to be signed in. A passkey is a resource of the person's own.
/// </summary>
internal static class PasskeyEndpoints
{
    const string PASSKEYS = "passkeys";

    public static IEndpointRouteBuilder MapPasskeys(this IEndpointRouteBuilder app)
    {
        // Signing in with a passkey is a session created with a credential: see AccountEndpoints.
        app.MapPost("/api/passkeys/actions/request-options", RequestOptions).AllowAnonymous();

        app.MapPost("/api/passkeys/actions/creation-options", CreationOptions).RequireAuthorization();
        app.MapGet("/api/passkeys", List).RequireAuthorization();
        app.MapPost("/api/passkeys", Create).RequireAuthorization();
        app.MapPatch("/api/passkeys/{id}", Rename).RequireAuthorization();
        app.MapDelete("/api/passkeys/{id}", Remove).RequireAuthorization();
        return app;
    }

    /// <summary>Validates the antiforgery token of a ceremony request. Null when it is good.</summary>
    public static async Task<IResult?> ValidateAntiforgeryAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return null;
        }
        catch (AntiforgeryValidationException)
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "antiforgery-token-invalid",
                "The request could not be verified. Reload the page and try again."
            );
        }
    }

    public static IResult NotConfigured()
    {
        return JsonApiResults.Error(
            StatusCodes.Status503ServiceUnavailable,
            "passkeys-not-configured",
            "Passkeys are not available on this host."
        );
    }

    public static object Describe(UserPasskeyInfo passkey)
    {
        return new
        {
            name = passkey.Name,
            createdAt = passkey.CreatedAt,
            transports = passkey.Transports,
            isBackedUp = passkey.IsBackedUp,
            isBackupEligible = passkey.IsBackupEligible,
        };
    }

    public static string IdOf(UserPasskeyInfo passkey)
    {
        return Base64Url.EncodeToString(passkey.CredentialId);
    }

    static async Task<IResult> RequestOptions(HttpContext context, IAntiforgery antiforgery, PasskeyService passkeys)
    {
        if (!passkeys.Availability.Enabled)
        {
            return NotConfigured();
        }

        if (await ValidateAntiforgeryAsync(context, antiforgery) is { } forged)
        {
            return forged;
        }

        var options = await passkeys.RequestOptionsAsync();
        return JsonApiResults.Resource(
            StatusCodes.Status200OK,
            "passkey-request-options",
            "current",
            new { options = JsonSerializer.Deserialize<JsonElement>(options) }
        );
    }

    static async Task<IResult> CreationOptions(
        HttpContext context,
        IAntiforgery antiforgery,
        PasskeyService passkeys,
        UserManager<NIdentityUser> users
    )
    {
        if (!passkeys.Availability.Enabled)
        {
            return NotConfigured();
        }

        if (await ValidateAntiforgeryAsync(context, antiforgery) is { } forged)
        {
            return forged;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return NotSignedIn();
        }

        var options = await passkeys.CreationOptionsAsync(user);
        return JsonApiResults.Resource(
            StatusCodes.Status200OK,
            "passkey-creation-options",
            "current",
            new { options = JsonSerializer.Deserialize<JsonElement>(options) }
        );
    }

    static async Task<IResult> List(HttpContext context, PasskeyService passkeys, UserManager<NIdentityUser> users)
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return NotSignedIn();
        }

        var list = await passkeys.ListAsync(user);
        return Results.Json(
            new
            {
                data = list.Select(x => new
                {
                    type = PASSKEYS,
                    id = IdOf(x),
                    attributes = Describe(x),
                }),
            },
            JsonApiResults.Options,
            JsonApiResults.MEDIA_TYPE
        );
    }

    static async Task<IResult> Create(
        HttpContext context,
        IAntiforgery antiforgery,
        PasskeyService passkeys,
        UserManager<NIdentityUser> users,
        AccountText text
    )
    {
        if (!passkeys.Availability.Enabled)
        {
            return NotConfigured();
        }

        if (await ValidateAntiforgeryAsync(context, antiforgery) is { } forged)
        {
            return forged;
        }

        var read = await JsonApiRequests.ReadAsync<PasskeyAttributes>(context.Request, PASSKEYS);
        if (read.Error != null)
        {
            return read.Error;
        }

        if (read.Attributes!.Credential is not { ValueKind: JsonValueKind.Object } credential)
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "malformed-request",
                "The request is malformed.",
                "Send the credential the browser made."
            );
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return NotSignedIn();
        }

        var passkey = await passkeys.EnrolAsync(
            user,
            credential.GetRawText(),
            read.Attributes.Name,
            text.Resolve(context.Request)
        );
        if (passkey is null)
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "invalid-passkey",
                "The passkey could not be verified."
            );
        }

        var id = IdOf(passkey);
        return JsonApiResults.Resource(
            StatusCodes.Status201Created,
            PASSKEYS,
            id,
            Describe(passkey),
            $"/api/passkeys/{id}"
        );
    }

    static async Task<IResult> Rename(
        HttpContext context,
        string id,
        PasskeyService passkeys,
        UserManager<NIdentityUser> users
    )
    {
        var read = await JsonApiRequests.ReadAsync<PasskeyAttributes>(context.Request, PASSKEYS);
        if (read.Error != null)
        {
            return read.Error;
        }

        if (!TryDecode(id, out var credentialId))
        {
            return NotFound();
        }

        var userId = users.GetUserId(context.User);
        if (!Guid.TryParse(userId, out var user))
        {
            return NotSignedIn();
        }

        var result = await passkeys.RenameAsync(user, credentialId, read.Attributes!.Name);
        if (!result.Succeeded)
        {
            return result.Errors.Any(x => x.Code == PasskeyService.NO_SUCH_PASSKEY)
                ? NotFound()
                : JsonApiResults.Error(StatusCodes.Status409Conflict, "conflict", "The passkey could not be renamed.");
        }

        var renamed = (await passkeys.ListAsync((await users.FindByIdAsync(userId))!)).First(x =>
            x.CredentialId.AsSpan().SequenceEqual(credentialId)
        );
        return JsonApiResults.Resource(StatusCodes.Status200OK, PASSKEYS, id, Describe(renamed));
    }

    static async Task<IResult> Remove(
        HttpContext context,
        string id,
        PasskeyService passkeys,
        UserManager<NIdentityUser> users
    )
    {
        if (!TryDecode(id, out var credentialId))
        {
            return NotFound();
        }

        if (!Guid.TryParse(users.GetUserId(context.User), out var user))
        {
            return NotSignedIn();
        }

        var result = await passkeys.RemoveAsync(user, credentialId);
        if (result.Succeeded)
        {
            return Results.NoContent();
        }

        if (result.Errors.Any(x => x.Code == PasskeyService.LAST_PASSKEY))
        {
            return JsonApiResults.Error(
                StatusCodes.Status409Conflict,
                "last-passkey",
                "The last passkey cannot be removed.",
                "Add another passkey first."
            );
        }

        return result.Errors.Any(x => x.Code == PasskeyService.NO_SUCH_PASSKEY)
            ? NotFound()
            : JsonApiResults.Error(StatusCodes.Status409Conflict, "conflict", "The passkey could not be removed.");
    }

    static IResult NotSignedIn()
    {
        return JsonApiResults.Error(StatusCodes.Status401Unauthorized, "not-signed-in", "Sign in to do this.");
    }

    static IResult NotFound()
    {
        return JsonApiResults.Error(StatusCodes.Status404NotFound, "not-found", "Not found");
    }

    static bool TryDecode(string id, out byte[] credentialId)
    {
        try
        {
            credentialId = Base64Url.DecodeFromChars(id);
            return credentialId.Length > 0;
        }
        catch (FormatException)
        {
            credentialId = [];
            return false;
        }
    }
}

internal sealed class PasskeyAttributes
{
    /// <summary>What the browser's WebAuthn API made, as its JSON form.</summary>
    public JsonElement? Credential { get; set; }

    public string? Name { get; set; }
}
