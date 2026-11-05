using System.Buffers.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NoTiming.Api.Application.Authentication.Account;
using NoTiming.Api.Application.Authentication.Email;
using NoTiming.Api.Application.Authentication.Passkeys;
using NoTiming.Api.Application.Authentication.Requests;
using NoTiming.Api.Application.Authentication.Sessions;
using NoTiming.Api.Application.Authentication.Store;

namespace NoTiming.Api.Controllers;

/// <summary>Passkey sign-in, and the passkeys of the signed-in user: enrolling, listing, renaming and removing.</summary>
[ApiController]
[Route("api")]
[ServiceFilter(typeof(StateChangeGuard))]
public sealed class PasskeyController : ControllerBase
{
    const int MAX_NAME_LENGTH = 60;

    readonly PasskeyService _passkeys;
    readonly AccountService _account;
    readonly IOptions<AuthenticationSettings> _settings;

    public PasskeyController(PasskeyService passkeys, AccountService account, IOptions<AuthenticationSettings> settings)
    {
        _passkeys = passkeys;
        _account = account;
        _settings = settings;
    }

    [HttpPost("sign-in/passkey/options")]
    public async Task<IActionResult> SignInOptions()
    {
        return Content(await _passkeys.RequestOptionsAsync(), "application/json");
    }

    /// <summary>Every failure gets the same answer, as a code check does.</summary>
    [HttpPost("sign-in/passkey/verify")]
    public async Task<IActionResult> SignInVerify([FromBody] JsonElement credential, CancellationToken cancellationToken)
    {
        if (credential.ValueKind != JsonValueKind.Object)
        {
            return Unauthorized(new { error = "passkey-not-accepted" });
        }

        var signIn = await _passkeys.SignInAsync(credential.GetRawText(), cancellationToken);
        if (signIn is null)
        {
            return Unauthorized(new { error = "passkey-not-accepted" });
        }

        Response.Cookies.Append(_settings.Value.CookieName, signIn.SessionToken, SessionCookie.Options(signIn.ExpiresAtUtc));
        var user = await _account.FindAsync(signIn.UserId, cancellationToken);
        return Ok(user is null ? MeResponse.Anonymous() : MeResponse.For(user));
    }

    [HttpPost("me/passkey/options")]
    public async Task<IActionResult> EnrolOptions(CancellationToken cancellationToken)
    {
        var userId = await SessionUser.IdOfAsync(HttpContext);
        if (userId is null)
        {
            return Unauthorized();
        }

        var options = await _passkeys.CreationOptionsAsync(userId.Value, cancellationToken);
        return options is null ? Conflict(new { error = "try-again" }) : Content(options, "application/json");
    }

    [HttpGet("me/passkeys")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = await SessionUser.IdOfAsync(HttpContext);
        if (userId is null)
        {
            return Unauthorized();
        }

        var passkeys = await _passkeys.ListAsync(userId.Value, cancellationToken);
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
        var userId = await SessionUser.IdOfAsync(HttpContext);
        if (userId is null)
        {
            return Unauthorized();
        }

        if (request.Credential.ValueKind != JsonValueKind.Object || !IsValidName(request.Name, allowEmpty: true))
        {
            return BadRequest(new { error = "invalid-passkey" });
        }

        var language = EmailLanguage.Choose(Request.Headers.AcceptLanguage.ToString());
        var outcome = await _passkeys.EnrolAsync(
            userId.Value,
            request.Credential.GetRawText(),
            request.Name,
            language,
            cancellationToken
        );
        return outcome.Kind == PasskeyOutcomeKind.Done
            ? Created(string.Empty, new { name = outcome.Name })
            : BadRequest(new { error = "passkey-not-accepted" });
    }

    [HttpPatch("me/passkeys/{id}")]
    public async Task<IActionResult> Rename(string id, [FromBody] RenamePasskeyRequest request, CancellationToken cancellationToken)
    {
        var userId = await SessionUser.IdOfAsync(HttpContext);
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

        var outcome = await _passkeys.RenameAsync(userId.Value, credentialId, request.Name!, cancellationToken);
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
        var userId = await SessionUser.IdOfAsync(HttpContext);
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!TryDecode(id, out var credentialId))
        {
            return NotFound();
        }

        var outcome = await _passkeys.RemoveAsync(userId.Value, credentialId, cancellationToken);
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
