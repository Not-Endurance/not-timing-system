using System.Buffers.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NC.Server.Authentication;
using NC.Server.Authentication.Passkeys;
using NC.Server.Authentication.Requests;

namespace NoTiming.Api.Controllers;

/// <summary>Passkey sign-in, and the passkeys of the signed-in user: enrolling, listing, renaming and removing.</summary>
[ApiController]
[Route("api")]
[ServiceFilter(typeof(StateChangeGuard))]
public sealed class PasskeyController : ControllerBase
{
    const int MAX_NAME_LENGTH = 60;

    readonly IAuthentication _authentication;
    readonly IPasskeyManagement _passkeys;

    public PasskeyController(IAuthentication authentication, IPasskeyManagement passkeys)
    {
        _authentication = authentication;
        _passkeys = passkeys;
    }

    [HttpPost("sign-in/passkey/options")]
    public async Task<IActionResult> SignInOptions(CancellationToken cancellationToken)
    {
        return Content(await _authentication.PasskeySignInOptions(cancellationToken), "application/json");
    }

    /// <summary>Every failure gets the same answer, as a code check does.</summary>
    [HttpPost("sign-in/passkey/verify")]
    public async Task<IActionResult> SignInVerify(
        [FromBody] JsonElement credential,
        CancellationToken cancellationToken
    )
    {
        if (credential.ValueKind != JsonValueKind.Object)
        {
            return Unauthorized(new { error = "passkey-not-accepted" });
        }

        var outcome = await _authentication.SignInWithPasskey(
            HttpContext,
            credential.GetRawText(),
            cancellationToken
        );
        return outcome.SignedIn && outcome.User is not null
            ? Ok(MeResponse.For(outcome.User))
            : Unauthorized(new { error = "passkey-not-accepted" });
    }

    [HttpPost("me/passkey/options")]
    public async Task<IActionResult> EnrolOptions(CancellationToken cancellationToken)
    {
        var userId = await _authentication.SignedInUserId(HttpContext, cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        var options = await _passkeys.CreationOptions(userId.Value, cancellationToken);
        return options is null ? Conflict(new { error = "try-again" }) : Content(options, "application/json");
    }

    [HttpGet("me/passkeys")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = await _authentication.SignedInUserId(HttpContext, cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        var passkeys = await _passkeys.ListPasskeys(userId.Value, cancellationToken);
        return Ok(
            passkeys.Select(x => new PasskeyItem
            {
                Id = Base64Url.EncodeToString(x.CredentialId),
                Name = x.Name,
                CreatedAt = x.CreatedAt,
            })
        );
    }

    [HttpPost("me/passkeys")]
    public async Task<IActionResult> Enrol([FromBody] EnrolPasskeyRequest request, CancellationToken cancellationToken)
    {
        var userId = await _authentication.SignedInUserId(HttpContext, cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        if (request.Credential.ValueKind != JsonValueKind.Object || !IsValidName(request.Name, allowEmpty: true))
        {
            return BadRequest(new { error = "invalid-passkey" });
        }

        var outcome = await _passkeys.Enrol(
            HttpContext,
            userId.Value,
            request.Credential.GetRawText(),
            request.Name,
            cancellationToken
        );
        return outcome.Kind == PasskeyOutcomeKind.Done
            ? Created(string.Empty, new { name = outcome.Name })
            : BadRequest(new { error = "passkey-not-accepted" });
    }

    [HttpPatch("me/passkeys/{id}")]
    public async Task<IActionResult> Rename(
        string id,
        [FromBody] RenamePasskeyRequest request,
        CancellationToken cancellationToken
    )
    {
        var userId = await _authentication.SignedInUserId(HttpContext, cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!TryDecode(id, out var credentialId))
        {
            return NotFound();
        }

        if (!IsValidName(request.Name, allowEmpty: false))
        {
            return BadRequest(new { error = "invalid-name" });
        }

        var outcome = await _passkeys.Rename(userId.Value, credentialId, request.Name!, cancellationToken);
        return outcome.Kind switch
        {
            PasskeyOutcomeKind.Done => NoContent(),
            PasskeyOutcomeKind.NotFound => NotFound(),
            _ => BadRequest(new { error = "passkey-not-renamed" }),
        };
    }

    [HttpDelete("me/passkeys/{id}")]
    public async Task<IActionResult> Remove(string id, CancellationToken cancellationToken)
    {
        var userId = await _authentication.SignedInUserId(HttpContext, cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!TryDecode(id, out var credentialId))
        {
            return NotFound();
        }

        var outcome = await _passkeys.Remove(userId.Value, credentialId, cancellationToken);
        return outcome.Kind switch
        {
            PasskeyOutcomeKind.Done => NoContent(),
            PasskeyOutcomeKind.NotFound => NotFound(),
            PasskeyOutcomeKind.LastPasskey => Conflict(new { error = "last-passkey" }),
            _ => BadRequest(new { error = "passkey-not-removed" }),
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
}

public sealed class EnrolPasskeyRequest
{
    public string? Name { get; init; }

    public JsonElement Credential { get; init; }
}

public sealed class RenamePasskeyRequest
{
    public string? Name { get; init; }
}

public sealed class PasskeyItem
{
    public string Id { get; init; } = string.Empty;

    public string? Name { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
