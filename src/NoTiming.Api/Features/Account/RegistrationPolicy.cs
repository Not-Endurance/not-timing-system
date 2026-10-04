namespace NoTiming.Api.Features.Account;

/// <summary>
/// Who may register (#601): any address, unless an allow-list is configured (<c>Registration:AllowList</c>, addresses
/// and <c>@domain</c> entries, separated by commas, semicolons or lines, or the entries of an array). Staging has the
/// list too, and without one it is closed, so that a host that is reachable by anyone does not send mail to anyone;
/// production without a list is open. The list is about new addresses only: an address that has an account signs in
/// whatever it says, and the answer to a registration never tells which it was.
/// </summary>
internal sealed class RegistrationPolicy
{
    static readonly char[] SEPARATORS = [',', ';', ' ', '\t', '\r', '\n'];
    public const string ALLOW_LIST = "Registration:AllowList";

    readonly HashSet<string> _addresses = new(StringComparer.Ordinal);
    readonly HashSet<string> _domains = new(StringComparer.Ordinal);
    readonly bool _restricted;

    public RegistrationPolicy(IConfiguration configuration, IHostEnvironment environment)
    {
        var section = configuration.GetSection(ALLOW_LIST);
        var entries = (section.Value ?? string.Empty)
            .Split(SEPARATORS, StringSplitOptions.RemoveEmptyEntries)
            .Concat(section.GetChildren().Select(x => x.Value ?? string.Empty))
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => x.Length > 0)
            .ToList();
        foreach (var entry in entries)
        {
            if (entry.StartsWith('@'))
            {
                _domains.Add(entry[1..]);
            }
            else
            {
                _addresses.Add(entry);
            }
        }

        _restricted = entries.Count > 0 || environment.IsStaging();
    }

    /// <summary>Whether a new person may register with the address, which is already normalized.</summary>
    public bool Allows(string normalizedEmail)
    {
        if (!_restricted)
        {
            return true;
        }

        var at = normalizedEmail.LastIndexOf('@');
        return _addresses.Contains(normalizedEmail) || (at >= 0 && _domains.Contains(normalizedEmail[(at + 1)..]));
    }
}
