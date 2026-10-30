namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// An email partly masked, as the searches and the lists of grants show one (ADR-0012): the first character of the name
/// and then three stars, and the domain. It lets a person tell two accounts of one name apart without handing out an
/// address that anyone could write to. What is not an email is masked altogether.
/// </summary>
internal static class EmailMask
{
    public static string Of(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "***";
        }

        var at = email.LastIndexOf('@');
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }
}
