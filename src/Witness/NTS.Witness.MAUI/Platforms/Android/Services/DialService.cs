using Android.Content;
using Application = Android.App.Application;
using Uri = Android.Net.Uri;

namespace NTS.Witness.Platforms.Services;

public class DialService : IDialService
{
    public void DialNumber(string phoneNumber)
    {
        var context = Application.Context;
        var uri = Uri.Parse($"tel:{phoneNumber}");
        var intent = new Intent(Intent.ActionDial, uri);
        intent.AddFlags(ActivityFlags.NewTask);
        context.StartActivity(intent);
    }
}
