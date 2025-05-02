using Not.Notify;
#if ANDROID

#endif

namespace NTS.Witness.Components.Pages;

public partial class EmergencyContacts
{
    public Dictionary<string, string> Contacts { get; set; } = default!;

    protected override void OnInitialized()
    {
        Contacts = new Dictionary<string, string>
        {
            { "Yo mama", "+359 882312321" },
            { "Baba yaga", "+359 666666666" }
        };
    }

    public void OnNumberClick(string phoneNumber)
    {
#if ANDROID
        DialAndroid(phoneNumber);
#elif IOS
        DialiOS(phoneNumber);
#else
        NotifyHelper.Warn("Dialing is not supported on desktop devices. It seems you are running in a test environment.");
#endif
    }

#if ANDROID
    public void DialAndroid(string phoneNumber)
    {
        var context = Android.App.Application.Context;
        var uri = Android.Net.Uri.Parse($"tel:{phoneNumber}");
        var intent = new Android.Content.Intent(Android.Content.Intent.ActionDial, uri);
        intent.AddFlags(Android.Content.ActivityFlags.NewTask);
        context.StartActivity(intent);
    }

#endif

#if IOS
    static void DialiOS(string phoneNumber)
    {
        var url = new Foundation.NSUrl($"tel:{phoneNumber}");
        if (UIKit.UIApplication.SharedApplication.CanOpenUrl(url))
        {
             UIKit.UIApplication.SharedApplication.OpenUrl(url, new Foundation.NSDictionary(), (success) =>
             {
                // Optional: handle success/failure of dialer launch
                Console.WriteLine($"Dialer opened: {success}");
             });
        }
    }

#endif
}

