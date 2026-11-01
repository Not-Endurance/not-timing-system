using System.Buffers.Text;
using System.Text.Json;

namespace NoTiming.Api.Features.Account;

internal sealed class PasskeyAttributes
{
    /// <summary>What the browser's WebAuthn API made, as its JSON form.</summary>
    public JsonElement? Credential { get; set; }

    public string? Name { get; set; }
}
