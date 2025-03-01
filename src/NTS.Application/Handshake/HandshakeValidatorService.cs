using System.Security.Cryptography;
using System.Text;
using Not.Injection;

namespace NTS.Application.Handshake;

public class HandshakeValidatorService : IHandshakeValidatorService
{
    // TODO: move key in the secrets repo
    const string KEY = "psst-secret-dont-tell-anyone";

    HMACSHA256 _sha;

    public HandshakeValidatorService()
    {
        _sha = new HMACSHA256(GetBytes(KEY));
    }

    public byte[] CreatePayload(string thisApp)
    {
        using var stream = new MemoryStream(GetBytes(thisApp));
        var result = _sha.ComputeHash(stream);
        return result;
    }

    public bool ValidatePayload(byte[] payload, string remoteApp)
    {
        using var stream = new MemoryStream(GetBytes(remoteApp));
        var expectedPayload = _sha.ComputeHash(stream);
        return Compare(payload, expectedPayload);
    }

    bool Compare(byte[] first, byte[] second)
    {
        if (first.Length != second.Length)
        {
            return false;
        }

        for (var i = 0; i < first.Length; i++)
        {
            if (first[i] != second[i])
            {
                return false;
            }
        }

        return true;
    }

    byte[] GetBytes(string content)
    {
        return Encoding.UTF8.GetBytes(content);
    }
}

public interface IHandshakeValidatorService : ITransient
{
    byte[] CreatePayload(string thisApp);
    bool ValidatePayload(byte[] payload, string remoteApp);
}
