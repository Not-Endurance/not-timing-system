using System.Buffers.Text;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// A passkey made without a browser: what an authenticator that gives no attestation ("none") sends back, with a key
/// of its own. The routes check it as they check any other (the challenge, the origin, the shape of the data), so a
/// test can add a passkey over HTTP where a browser would only be slower. The ceremonies a real authenticator takes
/// part in stay in <c>PasskeyCeremonyTests</c>.
/// </summary>
internal static class SoftwareAuthenticator
{
    /// <summary>The credential the browser would hand to the page after <c>navigator.credentials.create</c>.</summary>
    public static JsonElement Attestation(
        byte[] credentialId,
        string challenge,
        string relyingPartyId = "localhost",
        string origin = "https://localhost"
    )
    {
        var clientData = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                type = "webauthn.create",
                challenge,
                origin,
                crossOrigin = false,
            }
        );
        return JsonSerializer.SerializeToElement(
            new
            {
                id = Base64Url.EncodeToString(credentialId),
                rawId = Base64Url.EncodeToString(credentialId),
                type = "public-key",
                clientExtensionResults = new { },
                response = new
                {
                    clientDataJSON = Base64Url.EncodeToString(clientData),
                    attestationObject = Base64Url.EncodeToString(AttestationObject(credentialId, relyingPartyId)),
                    transports = new[] { "internal" },
                },
            }
        );
    }

    /// <summary>Asks for the options of a ceremony as the page does, and answers them with a passkey of this kind.</summary>
    public static async Task<HttpResponseMessage> AddPasskeyAsync(
        PageClient page,
        byte[] credentialId,
        string? name = null
    )
    {
        var options = await page.PostAsync("/api/passkeys/actions/creation-options");
        var challenge = (await ApiSessions.ReadJsonAsync(options))
            .GetProperty("data")
            .GetProperty("attributes")
            .GetProperty("options")
            .GetProperty("challenge")
            .GetString()!;
        return await page.WriteAsync(
            HttpMethod.Post,
            "/api/passkeys",
            "passkeys",
            new { credential = Attestation(credentialId, challenge), name }
        );
    }

    static byte[] AttestationObject(byte[] credentialId, string relyingPartyId)
    {
        // The authenticator data: the hash of the relying party, the flags (user present, user verified, attested
        // credential data follows), a sign count of zero, then the credential: no AAGUID, its id and its public key.
        var authenticatorData = new List<byte>();
        authenticatorData.AddRange(SHA256.HashData(Encoding.UTF8.GetBytes(relyingPartyId)));
        authenticatorData.Add(0x45);
        authenticatorData.AddRange(new byte[4]);
        authenticatorData.AddRange(new byte[16]);
        authenticatorData.Add((byte)(credentialId.Length >> 8));
        authenticatorData.Add((byte)(credentialId.Length & 0xFF));
        authenticatorData.AddRange(credentialId);
        authenticatorData.AddRange(PublicKey());

        var writer = new CborWriter(CborConformanceMode.Lax);
        writer.WriteStartMap(3);
        writer.WriteTextString("fmt");
        writer.WriteTextString("none");
        writer.WriteTextString("attStmt");
        writer.WriteStartMap(0);
        writer.WriteEndMap();
        writer.WriteTextString("authData");
        writer.WriteByteString(authenticatorData.ToArray());
        writer.WriteEndMap();
        return writer.Encode();
    }

    /// <summary>A fresh ES256 key in the COSE form of the specification.</summary>
    static byte[] PublicKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var point = key.ExportParameters(false).Q;
        var writer = new CborWriter(CborConformanceMode.Lax);
        writer.WriteStartMap(5);
        writer.WriteInt32(1); // key type: elliptic curve
        writer.WriteInt32(2);
        writer.WriteInt32(3); // algorithm: ES256
        writer.WriteInt32(-7);
        writer.WriteInt32(-1); // curve: P-256
        writer.WriteInt32(1);
        writer.WriteInt32(-2);
        writer.WriteByteString(point.X!);
        writer.WriteInt32(-3);
        writer.WriteByteString(point.Y!);
        writer.WriteEndMap();
        return writer.Encode();
    }
}
