using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NTS.Domain.Setup.Services.Identities;

/// <summary>
/// The keys a name is compared by when an Athlete or a Horse is matched (#635, ADR-0010). <see cref="Normalised"/> is the name
/// as it is written, trimmed, with its spaces collapsed and its case folded. <see cref="Strict"/> is that name in Latin letters
/// by the official Bulgarian-to-Latin system, which a Latin name already is: two names with the same strict key are one name in
/// two scripts, and an automatic match may rest on it. <see cref="Looser"/> also folds the spellings that people give one name
/// (ю as yu or u, я as ya or ia, ц as ts or c, щ as sht or shch, х as h or kh, the й of Nikolay as y, i or j, a letter with
/// a mark as without it): two names with the same looser key may be one name, and only a review may say so.
/// </summary>
public static partial class IdentityNames
{
    const int MAX_ROUNDS = 6;

    /// <summary>The name as it is written, trimmed, with runs of spaces made one and its case folded; nothing for a name that is empty.</summary>
    public static string Normalised(string? name)
    {
        return Spaces().Replace((name ?? string.Empty).Trim(), " ").ToUpperInvariant();
    }

    /// <summary>
    /// The normalised name in Latin letters by the official system of the Law on the Transliteration: each letter by its Latin
    /// letters (ж as zh, ц as ts, щ as sht, ъ as a, ю as yu, я as ya), the ending of a word that is ия as ia, and the soft
    /// sign as y. What is not a Bulgarian letter is left as it is.
    /// </summary>
    public static string Strict(string? name)
    {
        var text = Normalised(name);
        var latin = new StringBuilder(text.Length + 8);
        for (var index = 0; index < text.Length; index++)
        {
            var letter = text[index];
            if (letter == 'И' && index + 1 < text.Length && text[index + 1] == 'Я' && EndsAWord(text, index + 2))
            {
                latin.Append("IA");
                index++;
                continue;
            }

            latin.Append(LatinOf(letter) ?? letter.ToString());
        }

        return latin.ToString();
    }

    /// <summary>
    /// The strict key with the spelling variants folded to one: a mark on a letter goes, a dot, an apostrophe or a backtick goes
    /// and a hyphen is a space; ц as ts, tz or c is c, щ as sht or shch is sht, х as h or kh is h, x is ks, w is v; y and j are
    /// i (й as y, i or j; я as ya, ja or ia is ia), a doubled i is one (iya is ia), and ю as yu, iu, ju or u is u. The key of a
    /// key is the key: the folds are made again until they change nothing.
    /// </summary>
    public static string Looser(string? name)
    {
        var text = WithoutMarks(Strict(name));
        text = Spaces().Replace(Dividers().Replace(Removed().Replace(text, string.Empty), " "), " ").Trim();
        for (var round = 0; round < MAX_ROUNDS; round++)
        {
            var folded = Fold(text);
            if (folded == text)
            {
                break;
            }

            text = folded;
        }

        return text;
    }

    /// <summary>The words of the looser key in one order, so that a name given as "Testov Ivan" and as "Ivan Testov" is one.</summary>
    public static string Sorted(string? name)
    {
        var words = Looser(name).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Order(StringComparer.Ordinal));
    }

    static string Fold(string text)
    {
        var folded = text.Replace("SHCH", "SHT")
            .Replace("TZ", "C")
            .Replace("TS", "C")
            .Replace("KH", "H")
            .Replace("X", "KS")
            .Replace("W", "V");
        return folded.Replace('Y', 'I').Replace('J', 'I').Replace("II", "I").Replace("IU", "U");
    }

    static string WithoutMarks(string text)
    {
        var marks = new StringBuilder(text.Length);
        foreach (var letter in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(letter) != UnicodeCategory.NonSpacingMark)
            {
                marks.Append(letter);
            }
        }

        return marks.ToString().Normalize(NormalizationForm.FormC);
    }

    static bool EndsAWord(string text, int index)
    {
        return index >= text.Length || !char.IsLetter(text[index]);
    }

    static string? LatinOf(char letter)
    {
        return letter switch
        {
            'А' => "A",
            'Б' => "B",
            'В' => "V",
            'Г' => "G",
            'Д' => "D",
            'Е' => "E",
            'Ж' => "ZH",
            'З' => "Z",
            'И' => "I",
            'Й' => "Y",
            'К' => "K",
            'Л' => "L",
            'М' => "M",
            'Н' => "N",
            'О' => "O",
            'П' => "P",
            'Р' => "R",
            'С' => "S",
            'Т' => "T",
            'У' => "U",
            'Ф' => "F",
            'Х' => "H",
            'Ц' => "TS",
            'Ч' => "CH",
            'Ш' => "SH",
            'Щ' => "SHT",
            'Ъ' => "A",
            'Ь' => "Y",
            'Ю' => "YU",
            'Я' => "YA",
            _ => null,
        };
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"['’`.]")]
    private static partial Regex Removed();

    [GeneratedRegex(@"[-_/]")]
    private static partial Regex Dividers();
}
