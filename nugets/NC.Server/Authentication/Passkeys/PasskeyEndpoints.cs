using System.Buffers.Text;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NC.Server.Authentication.Account;
using NC.Server.Authentication.Interface;
using NC.Server.Authentication.Requests;

namespace NC.Server.Authentication.Passkeys;

/// <summary>Passkey sign-in, and the passkeys of the signed-in user: enrolling, listing, renaming and removing.</summary>
internal static class PasskeyEndpoints<TProfile>
    where TProfile : class, new()
{
    const int MAX_NAME_LENGTH = 60;

    internal static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/sign-in/passkey/options", SignInOptions).AddEndpointFilter<StateChangeGuard>();
        api.MapPost("/sign-in/passkey/verify", SignInVerify).AddEndpointFilter<StateChangeGuard>();
        api.MapPost("/me/passkey/options", EnrolOptions)
            .RequireAuthorization(AuthenticationPolicies.SIGNED_IN)
            .AddEndpointFilter<StateChangeGuard>();
        api.MapGet("/me/passkeys", ListPasskeys).RequireAuthorization(AuthenticationPolicies.SIGNED_IN);
        api.MapPost("/me/passkeys", Enrol)
            .RequireAuthorization(AuthenticationPolicies.SIGNED_IN)
            .AddEndpointFilter<StateChangeGuard>();
        api.MapPatch("/me/passkeys/{id}", Rename)
            .RequireAuthorization(AuthenticationPolicies.SIGNED_IN)
            .AddEndpointFilter<StateChangeGuard>();
        api.MapDelete("/me/passkeys/{id}", Remove)
            .RequireAuthorization(AuthenticationPolicies.SIGNED_IN)
            .AddEndpointFilter<StateChangeGuard>();
    }

    static async Task<IResult> SignInOptions(
        IAuthentication<TProfile> authentication,
        CancellationToken cancellationToken
    )
    {
        return Results.Content(await authentication.PasskeySignInOptions(cancellationToken), "application/json");
    }

    /// <summary>Every failure gets the same answer, as a code check does.</summary>
    static async Task<IResult> SignInVerify(
        JsonElement credential,
        HttpContext httpContext,
        IAuthentication<TProfile> authentication,
        CancellationToken cancellationToken
    )
    {
        if (credential.ValueKind != JsonValueKind.Object)
        {
            return Results.Json(new { error = "passkey-not-accepted" }, statusCode: StatusCodes.Status401Unauthorized);
        }

        var outcome = await authentication.SignInWithPasskey(
            httpContext,
            credential.GetRawText(),
            cancellationToken
        );
        return outcome.SignedIn && outcome.User is not null
            ? Results.Ok(MeResponse.For(outcome.User))
            : Results.Json(new { error = "passkey-not-accepted" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    static async Task<IResult> EnrolOptions(
        ClaimsPrincipal principal,
        IPasskeyManager passkeys,
        CancellationToken cancellationToken
    )
    {
        var userId = principal.GetSignedInUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var options = await passkeys.CreationOptions(userId.Value, cancellationToken);
        return options is null
            ? Results.Conflict(new { error = "try-again" })
            : Results.Content(options, "application/json");
    }

    static async Task<IResult> ListPasskeys(
        ClaimsPrincipal principal,
        IPasskeyManager passkeys,
        CancellationToken cancellationToken
    )
    {
        var userId = principal.GetSignedInUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var listed = await passkeys.List(userId.Value, cancellationToken);
        return Results.Ok(
            listed.Select(x => new PasskeyItem
            {
                Id = Base64Url.EncodeToString(x.CredentialId),
                Name = x.Name,
                CreatedAt = x.CreatedAt,
            })
        );
    }

    static async Task<IResult> Enrol(
        EnrolPasskeyRequest request,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        IPasskeyManager passkeys,
        CancellationToken cancellationToken
    )
    {
        var userId = principal.GetSignedInUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        if (request.Credential.ValueKind != JsonValueKind.Object || !IsValidName(request.Name, allowEmpty: true))
        {
            return Results.BadRequest(new { error = "invalid-passkey" });
        }

        var outcome = await passkeys.Enrol(
            httpContext,
            userId.Value,
            request.Credential.GetRawText(),
            request.Name,
            cancellationToken
        );
        return outcome.Kind == PasskeyOutcomeKind.Done
            ? Results.Created(string.Empty, new { name = outcome.Name })
            : Results.BadRequest(new { error = "passkey-not-accepted" });
    }

    static async Task<IResult> Rename(
        string id,
        RenamePasskeyRequest request,
        ClaimsPrincipal principal,
        IPasskeyManager passkeys,
        CancellationToken cancellationToken
    )
    {
        var userId = principal.GetSignedInUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        if (!TryDecode(id, out var credentialId))
        {
            return Results.NotFound();
        }

        if (!IsValidName(request.Name, allowEmpty: false))
        {
            return Results.BadRequest(new { error = "invalid-name" });
        }

        var outcome = await passkeys.Rename(userId.Value, credentialId, request.Name!, cancellationToken);
        return outcome.Kind switch
        {
            PasskeyOutcomeKind.Done => Results.NoContent(),
            PasskeyOutcomeKind.NotFound => Results.NotFound(),
            _ => Results.BadRequest(new { error = "passkey-not-renamed" }),
        };
    }

    static async Task<IResult> Remove(
        string id,
        ClaimsPrincipal principal,
        IPasskeyManager passkeys,
        CancellationToken cancellationToken
    )
    {
        var userId = principal.GetSignedInUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        if (!TryDecode(id, out var credentialId))
        {
            return Results.NotFound();
        }

        var outcome = await passkeys.Remove(userId.Value, credentialId, cancellationToken);
        return outcome.Kind switch
        {
            PasskeyOutcomeKind.Done => Results.NoContent(),
            PasskeyOutcomeKind.NotFound => Results.NotFound(),
            PasskeyOutcomeKind.LastPasskey => Results.Conflict(new { error = "last-passkey" }),
            _ => Results.BadRequest(new { error = "passkey-not-removed" }),
        };
    }

    static bool TryDecode(string id, out byte[] credentialId)
    {
        try
        {
            credentialId = Base64Url.DecodeFromChars(id);
            return true;
        }
        catch (FormatException)
        {
            credentialId = [];
            return false;
        }
    }

    static bool IsValidName(string? name, bool allowEmpty)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return allowEmpty;
        }

        return name.Trim().Length <= MAX_NAME_LENGTH;
    }

    internal sealed class EnrolPasskeyRequest
    {
        public string? Name { get; init; }

        public JsonElement Credential { get; init; }
    }

    internal sealed class RenamePasskeyRequest
    {
        public string? Name { get; init; }
    }

    internal sealed class PasskeyItem
    {
        public string Id { get; init; } = string.Empty;

        public string? Name { get; init; }

        public DateTimeOffset CreatedAt { get; init; }
    }
}
