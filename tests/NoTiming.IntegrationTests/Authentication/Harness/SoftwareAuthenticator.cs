using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NoTiming.IntegrationTests.Authentication.Harness;

/// <summary>
/// A platform authenticator in software, for the integration tests. It creates a passkey and answers sign-in challenges
/// the way a browser would, so the server runs its real WebAuthn checks: the challenge, the origin, the relying-party
/// hash, the flags and the signature. It uses "none" attestation, which is what the server asks for.
/// </summary>
internal sealed class SoftwareAuthenticator : IDisposable
{
    const int UserPresentAndVerified = 0x05;
    const int AttestedCredentialData = 0x40;

    readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    readonly byte[] _credentialId = RandomNumberGenerator.GetBytes(32);
    byte[] _userHandle = [];
    uint _signCount;

    /// <summary>Answers a creation challenge with a new passkey for the user the options name.</summary>
    public string Register(string optionsJson, string origin, string rpId)
    {
        using var options = JsonDocument.Parse(optionsJson);
        var root = options.RootElement;
        _userHandle = Base64Url.DecodeFromChars(root.GetProperty("user").GetProperty("id").GetString()!);

        var clientData = ClientData("webauthn.create", root.GetProperty("challenge").GetString()!, origin);
        var authenticatorData = AuthenticatorData(
            rpId,
            UserPresentAndVerified | AttestedCredentialData,
            attested: true,
            signCount: 0
        );
        var attestationObject = Cbor.Encode(
            [("fmt", "none"), ("attStmt", Array.Empty<(object, object)>()), ("authData", authenticatorData)]
        );

        return JsonSerializer.Serialize(
            new
            {
                id = Base64Url.EncodeToString(_credentialId),
                rawId = Base64Url.EncodeToString(_credentialId),
                type = "public-key",
                response = new
                {
                    clientDataJSON = Base64Url.EncodeToString(clientData),
                    attestationObject = Base64Url.EncodeToString(attestationObject),
                },
                clientExtensionResults = new { },
            }
        );
    }

    /// <summary>Answers a sign-in challenge with the passkey it holds, for the user it was registered to.</summary>
    public string Answer(string optionsJson, string origin, string rpId)
    {
        using var options = JsonDocument.Parse(optionsJson);
        var clientData = ClientData("webauthn.get", options.RootElement.GetProperty("challenge").GetString()!, origin);
        // The counter moves on every answer, as a real authenticator's does, so the server sees it increase.
        var authenticatorData = AuthenticatorData(
            rpId,
            UserPresentAndVerified,
            attested: false,
            signCount: ++_signCount
        );
        var signed = authenticatorData.Concat(SHA256.HashData(clientData)).ToArray();

        return JsonSerializer.Serialize(
            new
            {
                id = Base64Url.EncodeToString(_credentialId),
                rawId = Base64Url.EncodeToString(_credentialId),
                type = "public-key",
                response = new
                {
                    clientDataJSON = Base64Url.EncodeToString(clientData),
                    authenticatorData = Base64Url.EncodeToString(authenticatorData),
                    signature = Base64Url.EncodeToString(
                        _key.SignData(signed, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence)
                    ),
                    userHandle = Base64Url.EncodeToString(_userHandle),
                },
                clientExtensionResults = new { },
            }
        );
    }

    public void Dispose()
    {
        _key.Dispose();
    }

    static byte[] ClientData(string type, string challenge, string origin)
    {
        var json = JsonSerializer.Serialize(
            new
            {
                type,
                challenge,
                origin,
                crossOrigin = false,
            }
        );
        return Encoding.UTF8.GetBytes(json);
    }

    byte[] AuthenticatorData(string rpId, int flags, bool attested, uint signCount)
    {
        var data = new List<byte>(SHA256.HashData(Encoding.UTF8.GetBytes(rpId)))
        {
            (byte)flags,
            (byte)(signCount >> 24),
            (byte)(signCount >> 16),
            (byte)(signCount >> 8),
            (byte)signCount,
        };

        if (attested)
        {
            data.AddRange(new byte[16]);
            data.Add((byte)(_credentialId.Length >> 8));
            data.Add((byte)_credentialId.Length);
            data.AddRange(_credentialId);
            data.AddRange(CoseKey());
        }

        return data.ToArray();
    }

    byte[] CoseKey()
    {
        var point = _key.ExportParameters(false).Q;
        return Cbor.Encode([(1, 2), (3, -7), (-1, 1), (-2, point.X!), (-3, point.Y!)]);
    }

    /// <summary>The few CBOR types an attestation object and a COSE key use: integers, text, byte strings and maps.</summary>
    static class Cbor
    {
        public static byte[] Encode((object Key, object Value)[] map)
        {
            var buffer = new List<byte>();
            Write(buffer, map);
            return buffer.ToArray();
        }

        static void Write(List<byte> buffer, object value)
        {
            switch (value)
            {
                case int number when number >= 0:
                    Head(buffer, 0, number);
                    break;
                case int number:
                    Head(buffer, 1, -1 - number);
                    break;
                case string text:
                    var textBytes = Encoding.UTF8.GetBytes(text);
                    Head(buffer, 3, textBytes.Length);
                    buffer.AddRange(textBytes);
                    break;
                case byte[] bytes:
                    Head(buffer, 2, bytes.Length);
                    buffer.AddRange(bytes);
                    break;
                case (object Key, object Value)[] map:
                    Head(buffer, 5, map.Length);
                    foreach (var (key, entry) in map)
                    {
                        Write(buffer, key);
                        Write(buffer, entry);
                    }

                    break;
                default:
                    throw new NotSupportedException(value.GetType().Name);
            }
        }

        static void Head(List<byte> buffer, int majorType, int length)
        {
            var type = (byte)(majorType << 5);
            if (length < 24)
            {
                buffer.Add((byte)(type | length));
            }
            else if (length <= byte.MaxValue)
            {
                buffer.Add((byte)(type | 24));
                buffer.Add((byte)length);
            }
            else
            {
                buffer.Add((byte)(type | 25));
                buffer.Add((byte)(length >> 8));
                buffer.Add((byte)length);
            }
        }
    }
}
