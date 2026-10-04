using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.Features.Account;
using NoTiming.Api.JsonApi;
using NTS.Contracts.API;
using NTS.Domain.Aggregates;

namespace NoTiming.Api.Features.Profile;

/// <summary>
/// A signed-in person reads and edits their own profile (#602, ADR-0002, ADR-0012): country, names, club and FEI ID. The
/// routes have no id: they act on the caller and on nobody else, and a body that names another account is refused. A
/// profile needs a country, a first name and a surname. Editing it never moves the home Tenant; a person who has none
/// yet is placed when they first pick a country, which is the only thing a profile can do to it.
/// </summary>
internal static class ProfileEndpoints
{
    const string PROFILES = "profiles";
    const int MAX_NAME_LENGTH = 100;
    const int MAX_FEI_ID_LENGTH = 20;

    public static IEndpointRouteBuilder MapProfile(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me/profile", Read).RequireAuthorization();
        app.MapPatch("/api/me/profile", Edit).RequireAuthorization();
        return app;
    }

    /// <summary>The profile as a resource: the account's id is its id, and what is not set is not there.</summary>
    public static IResult ResourceOf(NIdentityUser user, IReadOnlyList<Country> countries, int status)
    {
        var region = user.TextOf("CountryRegion");
        var country = region == null ? null : countries.FirstOrDefault(x => x.Matches(region));
        return JsonApiResults.Resource(
            status,
            PROFILES,
            user.Id.ToString(),
            new
            {
                givenName = user.TextOf("GivenName"),
                middleName = user.TextOf("MiddleName"),
                surname = user.TextOf("Surname"),
                countryId = country?.Id,
                countryRegion = region,
                club = user.TextOf("Club"),
                feiId = user.TextOf("FeiId"),
                complete = IsComplete(user),
            }
        );
    }

    /// <summary>The rule of the profile screen: a first name, a surname and a country that the profile names.</summary>
    public static bool IsComplete(NIdentityUser user)
    {
        return user.TextOf("GivenName") != null
            && user.TextOf("Surname") != null
            && user.TextOf("CountryRegion") != null;
    }

    static async Task<IResult> Read(
        HttpContext context,
        UserManager<NIdentityUser> users,
        SelectableCountries countries
    )
    {
        var user = await users.GetUserAsync(context.User);
        return user is null
            ? JsonApiResults.NotSignedIn()
            : ResourceOf(user, await countries.AllAsync(context.RequestAborted), StatusCodes.Status200OK);
    }

    static async Task<IResult> Edit(
        HttpContext context,
        UserManager<NIdentityUser> users,
        SelectableCountries countries,
        ProfileStore store,
        TenantPlacement tenants
    )
    {
        var read = await JsonApiRequests.ReadAsync<ProfileAttributes>(context.Request, PROFILES);
        if (read.Error != null)
        {
            return read.Error;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        // The route names nobody, so a document that names an account must name the caller's: it is never a way to
        // reach another.
        if (read.Id != null && read.Id != user.Id.ToString())
        {
            return JsonApiResults.Error(
                StatusCodes.Status409Conflict,
                "profile-id-mismatch",
                "The profile is the caller's own: the id of the resource is not theirs."
            );
        }

        // Members are found whatever their case, as those of every other document are (the Web options of the Api).
        var members = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in read.Attributes!.Members ?? [])
        {
            members[name] = value;
        }

        var merged = await MergeAsync(user, members, countries, context.RequestAborted);
        if (merged.Refusal != null)
        {
            return merged.Refusal;
        }

        var profile = merged.Profile!;
        if (!profile.HasRequiredProfile())
        {
            return JsonApiResults.Error(
                StatusCodes.Status422UnprocessableEntity,
                "profile-incomplete",
                "A profile needs a country, a first name and a surname."
            );
        }

        await store.SaveAsync(user.Id, ChangesOf(user, profile), context.RequestAborted);

        // Only a person who has no home Tenant gets one, from the country they have just picked; the rest is left as it is.
        var saved = (await users.FindByIdAsync(user.Id.ToString()))!;
        await tenants.EnsureHomeTenantAsync(saved, context.RequestAborted);
        var after = (await users.FindByIdAsync(user.Id.ToString()))!;
        return ResourceOf(after, await countries.AllAsync(context.RequestAborted), StatusCodes.Status200OK);
    }

    /// <summary>
    /// The fields of the document that the edit changes, and only those. The name is made again from the three names
    /// when one of them changes, and left as it is otherwise.
    /// </summary>
    static Dictionary<string, string?> ChangesOf(NIdentityUser user, UpdateUserProfilePayload profile)
    {
        var changes = new Dictionary<string, string?>();
        Change(changes, user, "GivenName", profile.GivenName);
        Change(changes, user, "MiddleName", profile.MiddleName);
        Change(changes, user, "Surname", profile.Surname);
        Change(changes, user, "CountryRegion", profile.CountryRegion);
        Change(changes, user, "Club", profile.Club);
        Change(changes, user, "FeiId", profile.FeiId);
        if (changes.ContainsKey("GivenName") || changes.ContainsKey("MiddleName") || changes.ContainsKey("Surname"))
        {
            changes["Name"] = profile.Name;
        }

        return changes;
    }

    static void Change(Dictionary<string, string?> changes, NIdentityUser user, string field, string? value)
    {
        if (user.TextOf(field) != value)
        {
            changes[field] = value;
        }
    }

    /// <summary>
    /// What the person has once the members of the document are applied: a member that is not named stays as it is, an
    /// optional one that is null or blank is cleared, and the first member that is not valid refuses the whole edit.
    /// </summary>
    static async Task<MergedProfile> MergeAsync(
        NIdentityUser user,
        Dictionary<string, JsonElement> members,
        SelectableCountries countries,
        CancellationToken cancellationToken
    )
    {
        var givenName = user.TextOf("GivenName");
        var middleName = user.TextOf("MiddleName");
        var surname = user.TextOf("Surname");
        var region = user.TextOf("CountryRegion");
        var club = user.TextOf("Club");
        var feiId = user.TextOf("FeiId");

        if (
            !Apply(members, "givenName", MAX_NAME_LENGTH, required: true, ref givenName)
            || !Apply(members, "surname", MAX_NAME_LENGTH, required: true, ref surname)
            || !Apply(members, "middleName", MAX_NAME_LENGTH, required: false, ref middleName)
        )
        {
            return MergedProfile.Refused("invalid-name", "A name is at most 100 characters of one line.");
        }

        if (!Apply(members, "club", MAX_NAME_LENGTH, required: false, ref club))
        {
            return MergedProfile.Refused("invalid-club", "A club is at most 100 characters of one line.");
        }

        if (!Apply(members, "feiId", MAX_FEI_ID_LENGTH, required: false, ref feiId))
        {
            return MergedProfile.Refused("invalid-fei-id", "An FEI ID is at most 20 characters of one line.");
        }

        if (members.TryGetValue("countryId", out var member))
        {
            var country =
                member.ValueKind == JsonValueKind.String && Guid.TryParse(member.GetString(), out var id)
                    ? await countries.FindAsync(id, cancellationToken)
                    : null;
            if (country is null)
            {
                return MergedProfile.Refused("invalid-country", "Choose a country from the list.");
            }

            region = country.Name;
        }

        return MergedProfile.Of(new UpdateUserProfilePayload(givenName, surname, region, middleName, club, feiId));
    }

    /// <summary>
    /// Sets a text member from the document when it is named: null or blank clears it, and that is refused for a required
    /// one. False when the member is not a text of one line that is short enough.
    /// </summary>
    static bool Apply(
        Dictionary<string, JsonElement> members,
        string name,
        int maxLength,
        bool required,
        ref string? value
    )
    {
        if (!members.TryGetValue(name, out var member))
        {
            return true;
        }

        if (member.ValueKind == JsonValueKind.Null)
        {
            value = null;
            return !required;
        }

        if (member.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = member.GetString()!.Trim();
        if (!OneLineText.IsValid(text, maxLength))
        {
            return false;
        }

        value = text.Length == 0 ? null : text;
        return !required || value != null;
    }

    sealed class MergedProfile
    {
        public static MergedProfile Of(UpdateUserProfilePayload profile)
        {
            return new MergedProfile(profile, null);
        }

        public static MergedProfile Refused(string code, string title)
        {
            return new MergedProfile(null, JsonApiResults.Error(StatusCodes.Status400BadRequest, code, title));
        }

        MergedProfile(UpdateUserProfilePayload? profile, IResult? refusal)
        {
            Profile = profile;
            Refusal = refusal;
        }

        public UpdateUserProfilePayload? Profile { get; }
        public IResult? Refusal { get; }
    }
}

/// <summary>
/// The members of a profile that a PATCH names. They are kept as they were sent, because "not named" and "null" mean
/// different things: the first leaves a member alone and the second clears it.
/// </summary>
internal sealed class ProfileAttributes
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Members { get; set; }
}
