using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NTS.Tools.IdentityMigration;

/// <summary>
/// The Guid a legacy integer id becomes (#637, ADR-0009): a name-based UUID (RFC 4122 version 5, SHA-1) of the id space, the parent
/// the id is scoped to and the integer, so that the conversion is a pure function. It stores nothing: a run that stopped finishes
/// when it is run again, a rehearsal on a restored copy gives the Guids that production will get, and two machines agree.
/// </summary>
public static class LegacyGuids
{
    /// <summary>The namespace of every name: the version 5 UUID of a URL that names this repository.</summary>
    static readonly Guid Namespace = Guid.Parse("68682499-7fe8-561a-b1ae-1f4372a8f1cf");

    /// <param name="space">The kind of thing the id is of; copies of one thing share it.</param>
    /// <param name="scope">The Guid of the parent the id is scoped to, none for an id that is not.</param>
    /// <param name="legacy">The integer the thing was called.</param>
    public static Guid Of(string space, Guid? scope, long legacy)
    {
        return Of(space, scope, legacy.ToString(CultureInfo.InvariantCulture));
    }

    /// <param name="space">The kind of thing the id is of; copies of one thing share it.</param>
    /// <param name="scope">The Guid of the parent the id is scoped to, none for an id that is not.</param>
    /// <param name="key">What the thing is called: an integer as text, or a text such as <c>fei:100</c> that no integer can be.</param>
    public static Guid Of(string space, Guid? scope, string key)
    {
        var name = Encoding.UTF8.GetBytes($"{space}/{scope?.ToString("D")}/{key}");
        var hash = SHA1.HashData([.. Namespace.ToByteArray(bigEndian: true), .. name]);
        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }
}
