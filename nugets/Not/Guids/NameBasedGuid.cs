using System.Security.Cryptography;
using System.Text;

namespace Not.Guids;

/// <summary>
/// A name-based Guid (RFC 4122, version 5): the same namespace and name make the same Guid on every machine and in every run,
/// so that a record can be found again by what it is named for. The namespace and the name are hashed with SHA-1, as the
/// standard says.
/// </summary>
public static class NameBasedGuid
{
    const int VERSION = 5;

    public static Guid Create(Guid namespaceId, string name)
    {
        var hash = SHA1.HashData([.. SwapFieldOrder(namespaceId.ToByteArray()), .. Encoding.UTF8.GetBytes(name)]);

        var bytes = hash.AsSpan(0, 16).ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | (VERSION << 4));
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(SwapFieldOrder(bytes));
    }

    /// <summary>
    /// RFC 4122 writes the first three fields of a Guid big-endian, and .NET stores them little-endian. Swapping them is its own
    /// inverse, so the same call goes both ways.
    /// </summary>
    static byte[] SwapFieldOrder(byte[] bytes)
    {
        Array.Reverse(bytes, 0, 4);
        Array.Reverse(bytes, 4, 2);
        Array.Reverse(bytes, 6, 2);
        return bytes;
    }
}
