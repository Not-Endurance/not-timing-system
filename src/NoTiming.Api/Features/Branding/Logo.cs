using System.Text.Json;
using NTS.Contracts.Branding;

namespace NoTiming.Api.Features.Branding;

/// <summary>
/// A logo that a Tenant or an Event puts on what it prints (#646, ADR-0012): an image of the kinds a browser prints, of at most
/// <see cref="MAX_BYTES"/>, which is what the Api takes and what it keeps. It is told apart from a file that only says it is an image
/// by what the file starts with, because a logo is shown by every person who prints a document of the Event.
/// </summary>
internal sealed class Logo
{
    static readonly Dictionary<string, Func<byte[], bool>> KINDS = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = bytes => StartsWith(bytes, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
        ["image/jpeg"] = bytes => StartsWith(bytes, 0xFF, 0xD8, 0xFF),
        ["image/gif"] = bytes => StartsWith(bytes, 0x47, 0x49, 0x46, 0x38),
        ["image/webp"] = bytes =>
            StartsWith(bytes, 0x52, 0x49, 0x46, 0x46)
            && bytes.Length > 12
            && StartsWith(bytes[8..], 0x57, 0x45, 0x42, 0x50),
    };
    public const int MAX_BYTES = LogoLimits.MAX_BYTES;

    /// <summary>The logo a document names, or what is wrong with it: a malformed document is 400 <c>invalid-logo</c> and one that weighs too much is 422 <c>logo-too-large</c>.</summary>
    public static LogoRead Read(JsonElement document)
    {
        if (document.ValueKind != JsonValueKind.Object)
        {
            return LogoRead.Refused(
                StatusCodes.Status400BadRequest,
                "invalid-logo",
                "A logo is an object with the contentType and the content of an image."
            );
        }

        string? contentType = null;
        string? content = null;
        foreach (var member in document.EnumerateObject())
        {
            if (string.Equals(member.Name, "contentType", StringComparison.OrdinalIgnoreCase))
            {
                contentType = member.Value.ValueKind == JsonValueKind.String ? member.Value.GetString() : null;
            }
            else if (string.Equals(member.Name, "content", StringComparison.OrdinalIgnoreCase))
            {
                content = member.Value.ValueKind == JsonValueKind.String ? member.Value.GetString() : null;
            }
            else if (
                string.Equals(member.Name, "size", StringComparison.OrdinalIgnoreCase)
                || string.Equals(member.Name, "source", StringComparison.OrdinalIgnoreCase)
            )
            {
                // what the Api computes and tells, which a client that sends a logo back as it read it sends too: writes ignore it
            }
            else
            {
                return LogoRead.Refused(
                    StatusCodes.Status400BadRequest,
                    "invalid-logo",
                    "A logo is an object with the contentType and the content of an image and nothing else (the size and the source are the Api's, and are ignored)."
                );
            }
        }

        if (contentType is null || !KINDS.TryGetValue(contentType.Trim(), out var isOfKind))
        {
            return LogoRead.Refused(
                StatusCodes.Status400BadRequest,
                "invalid-logo",
                $"The contentType of a logo is one of {string.Join(", ", KINDS.Keys)}."
            );
        }

        if (string.IsNullOrEmpty(content))
        {
            return LogoRead.Refused(
                StatusCodes.Status400BadRequest,
                "invalid-logo",
                "The content of a logo is the image, as base64."
            );
        }

        // the text is longer than the image by a third: one that is longer than the limit allows is refused before it is decoded
        if (content.Length > (MAX_BYTES + 2) / 3 * 4)
        {
            return TooLarge();
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(content);
        }
        catch (FormatException)
        {
            return LogoRead.Refused(
                StatusCodes.Status400BadRequest,
                "invalid-logo",
                "The content of a logo is the image, as base64."
            );
        }

        if (bytes.Length > MAX_BYTES)
        {
            return TooLarge();
        }

        return bytes.Length > 0 && isOfKind(bytes)
            ? LogoRead.Taken(new Logo(contentType.Trim().ToLowerInvariant(), bytes))
            : LogoRead.Refused(
                StatusCodes.Status400BadRequest,
                "invalid-logo",
                "The content is not an image of the contentType it names."
            );
    }

    public Logo(string contentType, byte[] content)
    {
        ContentType = contentType;
        Content = content;
    }

    public string ContentType { get; }
    public byte[] Content { get; }

    static LogoRead TooLarge()
    {
        return LogoRead.Refused(
            StatusCodes.Status422UnprocessableEntity,
            "logo-too-large",
            $"A logo weighs at most {MAX_BYTES / 1024} KiB."
        );
    }

    static bool StartsWith(byte[] bytes, params byte[] start)
    {
        return bytes.Length >= start.Length && bytes.AsSpan(0, start.Length).SequenceEqual(start);
    }
}

/// <summary>What a document that names a logo comes to: the logo, or the answer that refuses it.</summary>
internal sealed class LogoRead
{
    public static LogoRead Taken(Logo logo)
    {
        return new LogoRead(logo, 0, null, null);
    }

    public static LogoRead Refused(int status, string code, string detail)
    {
        return new LogoRead(null, status, code, detail);
    }

    LogoRead(Logo? logo, int status, string? code, string? detail)
    {
        Logo = logo;
        Status = status;
        Code = code;
        Detail = detail;
    }

    public Logo? Logo { get; }
    public int Status { get; }
    public string? Code { get; }
    public string? Detail { get; }
}
