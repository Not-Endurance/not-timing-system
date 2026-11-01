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
        // Signing in with a passkey is a session created with a credential: see SessionEndpoints.
        app.MapPost("/api/passkeys/actions/request-options", PostPasskeysActionsRequestOptions);

        app.MapPost("/api/passkeys/actions/creation-options", PostPasskeysActionsCreationOptions);
        app.MapGet("/api/passkeys", GetPasskeys);
        app.MapPost("/api/passkeys", PostPasskeys);
        app.MapPatch("/api/passkeys/{id}", PatchPasskeysById);
        app.MapDelete("/api/passkeys/{id}", DeletePasskeysById);
        return app;
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

    static async Task<IResult> PostPasskeysActionsRequestOptions(
        HttpContext context,
        IAntiforgery antiforgery,
        PasskeyService passkeys
    )
    {
        if (await PasskeyCeremony.RefuseAsync(context, passkeys, antiforgery) is { } refusal)
        {
            return refusal;
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
    [Requires(Capability.SignedIn)]
    static async Task<IResult> PostPasskeysActionsCreationOptions(
        HttpContext context,
        IAntiforgery antiforgery,
        PasskeyService passkeys,
        AccessContext access
    )
    {
        if (await PasskeyCeremony.RefuseAsync(context, passkeys, antiforgery) is { } refusal)
        {
            return refusal;
        }

        var options = await passkeys.CreationOptionsAsync(access.User);
        return JsonApiResults.Resource(
            StatusCodes.Status200OK,
            "passkey-creation-options",
            "current",
            new { options = JsonSerializer.Deserialize<JsonElement>(options) }
        );
    }

    [Requires(Capability.SignedIn)]
    static async Task<IResult> GetPasskeys(PasskeyService passkeys, AccessContext access)
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

    [Requires(Capability.SignedIn)]
    static async Task<IResult> PostPasskeys(
        HttpContext context,
        IAntiforgery antiforgery,
        PasskeyService passkeys,
        AccessContext access,
        AccountText text
    )
    {
        if (await PasskeyCeremony.RefuseAsync(context, passkeys, antiforgery) is { } refusal)
        {
            return refusal;
        }

        var read = await JsonApiRequests.ReadAsync<PasskeyAttributes>(context.Request, PASSKEYS);
        if (read.Error != null)
        {
            return read.Error;
        }

        if (read.Attributes!.Credential is not { ValueKind: JsonValueKind.Object } credential)
        {
            return JsonApiFailures.MalformedRequest.With("Send the credential the browser made.").AsResult();
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
            return AccountFailures.PasskeyNotVerified.AsResult();
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

    [Requires(Capability.SignedIn)]
    static async Task<IResult> PatchPasskeysById(
        HttpContext context,
        string id,
        PasskeyService passkeys,
        AccessContext access
    )
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
                : AccountFailures.PasskeyRenameConflict.AsResult();
        }

        return JsonApiResults.Resource(StatusCodes.Status200OK, PASSKEYS, id, Describe(renamed));
    }

    [Requires(Capability.SignedIn)]
    static async Task<IResult> DeletePasskeysById(string id, PasskeyService passkeys, AccessContext access)
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
            return AccountFailures.LastPasskey.AsResult();
        }

        return result.Errors.Any(x => x.Code == PasskeyService.NO_SUCH_PASSKEY)
            ? JsonApiResults.NotFound()
            : AccountFailures.PasskeyRemoveConflict.AsResult();
    }

    /// <summary>The page limits the name too, but a client need not use the page. Null when the name is good.</summary>
    static IResult? InvalidName(string? name)
    {
        return name is not null && name.Trim().Length > MAX_NAME_LENGTH
            ? AccountFailures.InvalidPasskeyName(MAX_NAME_LENGTH).AsResult()
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
