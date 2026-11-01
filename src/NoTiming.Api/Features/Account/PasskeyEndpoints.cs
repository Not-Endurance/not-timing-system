using System.Buffers.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using NoTiming.Api.Features.Access;
using NoTiming.Api.JsonApi;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// The passkey routes (ADR-0002). The ceremony routes are ported from the reference implementation of Identity in
/// .NET 10 and keep what it requires of them: the options and the credential are posted with an antiforgery token, and
/// the ones that act for a person need that person to be signed in. A passkey is a resource of the person's own.
/// </summary>
internal static class PasskeyEndpoints
{
    const string PASSKEYS = "passkeys";
    const int MAX_NAME_LENGTH = 60;

    public static IEndpointRouteBuilder MapPasskeys(this IEndpointRouteBuilder app)
    {
        // Signing in with a passkey is a session created with a credential: see AccountEndpoints.
        app.MapPost("/api/passkeys/actions/request-options", RequestOptions);

        app.MapPost("/api/passkeys/actions/creation-options", CreationOptions);
        app.MapGet("/api/passkeys", List);
        app.MapPost("/api/passkeys", Create);
        app.MapPatch("/api/passkeys/{id}", Rename);
        app.MapDelete("/api/passkeys/{id}", Remove);
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

    // The ceremony has an order of its own: the host supports passkeys, the token is good, and only then the document.
    [Requires(Capability.SignedIn, PlaceIn.Platform)]
    static async Task<IResult> CreationOptions(
        HttpContext context,
        IAntiforgery antiforgery,
        PasskeyService passkeys,
        AccessContext access
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

        var options = await passkeys.CreationOptionsAsync(access.User);
        return JsonApiResults.Resource(
            StatusCodes.Status200OK,
            "passkey-creation-options",
            "current",
            new { options = JsonSerializer.Deserialize<JsonElement>(options) }
        );
    }

    [Requires(Capability.SignedIn, PlaceIn.Platform)]
    static async Task<IResult> List(PasskeyService passkeys, AccessContext access)
    {
        var list = await passkeys.ListAsync(access.User);
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

    [Requires(Capability.SignedIn, PlaceIn.Platform)]
    static async Task<IResult> Create(
        HttpContext context,
        IAntiforgery antiforgery,
        PasskeyService passkeys,
        AccessContext access,
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

        if (InvalidName(read.Attributes.Name) is { } invalidName)
        {
            return invalidName;
        }

        var passkey = await passkeys.EnrolAsync(
            access.User,
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

    [Requires(Capability.SignedIn, PlaceIn.Platform)]
    static async Task<IResult> Rename(HttpContext context, string id, PasskeyService passkeys, AccessContext access)
    {
        var read = await JsonApiRequests.ReadAsync<PasskeyAttributes>(context.Request, PASSKEYS);
        if (read.Error != null)
        {
            return read.Error;
        }

        if (InvalidName(read.Attributes!.Name) is { } invalidName)
        {
            return invalidName;
        }

        if (!TryDecode(id, out var credentialId))
        {
            return JsonApiResults.NotFound();
        }

        var (result, renamed) = await passkeys.RenameAsync(access.User.Id, credentialId, read.Attributes!.Name);
        if (renamed is null)
        {
            return result.Errors.Any(x => x.Code == PasskeyService.NO_SUCH_PASSKEY)
                ? JsonApiResults.NotFound()
                : JsonApiResults.Error(StatusCodes.Status409Conflict, "conflict", "The passkey could not be renamed.");
        }

        return JsonApiResults.Resource(StatusCodes.Status200OK, PASSKEYS, id, Describe(renamed));
    }

    [Requires(Capability.SignedIn, PlaceIn.Platform)]
    static async Task<IResult> Remove(string id, PasskeyService passkeys, AccessContext access)
    {
        if (!TryDecode(id, out var credentialId))
        {
            return JsonApiResults.NotFound();
        }

        var result = await passkeys.RemoveAsync(access.User.Id, credentialId);
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
            ? JsonApiResults.NotFound()
            : JsonApiResults.Error(StatusCodes.Status409Conflict, "conflict", "The passkey could not be removed.");
    }

    /// <summary>The page limits the name too, but a client need not use the page. Null when the name is good.</summary>
    static IResult? InvalidName(string? name)
    {
        return name is not null && name.Trim().Length > MAX_NAME_LENGTH
            ? JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "invalid-name",
                "The name is too long.",
                $"Use at most {MAX_NAME_LENGTH} characters."
            )
            : null;
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
