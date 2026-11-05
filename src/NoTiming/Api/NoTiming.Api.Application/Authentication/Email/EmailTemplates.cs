using System.Globalization;

namespace NoTiming.Api.Application.Authentication.Email;

/// <summary>The texts of the emails that sign-in sends, in English, Bulgarian and Turkish.</summary>
public static class EmailTemplates
{
    public static EmailMessage SignInCode(string to, string language, string code)
    {
        var text = Texts(language);
        return new EmailMessage
        {
            To = to,
            Subject = text.SignInSubject,
            TextBody = string.Format(CultureInfo.InvariantCulture, text.SignInBody, code),
        };
    }

    public static EmailMessage RegistrationCode(string to, string language, string code)
    {
        var text = Texts(language);
        return new EmailMessage
        {
            To = to,
            Subject = text.RegistrationSubject,
            TextBody = string.Format(CultureInfo.InvariantCulture, text.RegistrationBody, code),
        };
    }

    public static EmailMessage PasskeyAdded(string to, string language, string passkeyName, DateTimeOffset addedUtc)
    {
        var text = Texts(language);
        return new EmailMessage
        {
            To = to,
            Subject = text.PasskeySubject,
            TextBody = string.Format(
                CultureInfo.InvariantCulture,
                text.PasskeyBody,
                addedUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC",
                passkeyName
            ),
        };
    }

    static Wording Texts(string language)
    {
        return language switch
        {
            "bg" => new Wording(
                "Вашият код за вход",
                "Вашият код за вход е {0}. Важи 10 минути и може да се използва само веднъж. Ако не сте го поискали, игнорирайте този имейл.",
                "Вашият код за регистрация",
                "Вашият код за регистрация е {0}. Важи 10 минути и може да се използва само веднъж. Ако не сте се регистрирали, игнорирайте този имейл.",
                "Добавен е нов ключ за достъп",
                "На {0} към профила ви беше добавен ключ за достъп: {1}. Ако не сте го добавили вие, влезте и го премахнете."
            ),
            "tr" => new Wording(
                "Giriş kodunuz",
                "Giriş kodunuz: {0}. Kod 10 dakika geçerlidir ve yalnızca bir kez kullanılabilir. Bunu siz istemediyseniz bu e-postayı yok sayın.",
                "Kayıt kodunuz",
                "Kayıt kodunuz: {0}. Kod 10 dakika geçerlidir ve yalnızca bir kez kullanılabilir. Kayıt olmadıysanız bu e-postayı yok sayın.",
                "Hesabınıza yeni bir geçiş anahtarı eklendi",
                "{0} tarihinde hesabınıza bir geçiş anahtarı eklendi: {1}. Bunu siz eklemediyseniz oturum açıp anahtarı kaldırın."
            ),
            _ => new Wording(
                "Your sign-in code",
                "Your sign-in code is {0}. It expires in 10 minutes and works once. If you did not ask for it, ignore this email.",
                "Your registration code",
                "Your registration code is {0}. It expires in 10 minutes and works once. If you did not register, ignore this email.",
                "A passkey was added to your account",
                "A passkey named {1} was added to your account on {0}. If you did not add it, sign in and remove it."
            ),
        };
    }

    sealed class Wording
    {
        public Wording(
            string signInSubject,
            string signInBody,
            string registrationSubject,
            string registrationBody,
            string passkeySubject,
            string passkeyBody
        )
        {
            SignInSubject = signInSubject;
            SignInBody = signInBody;
            RegistrationSubject = registrationSubject;
            RegistrationBody = registrationBody;
            PasskeySubject = passkeySubject;
            PasskeyBody = passkeyBody;
        }

        public string SignInSubject { get; }

        public string SignInBody { get; }

        public string RegistrationSubject { get; }

        public string RegistrationBody { get; }

        public string PasskeySubject { get; }

        public string PasskeyBody { get; }
    }
}
