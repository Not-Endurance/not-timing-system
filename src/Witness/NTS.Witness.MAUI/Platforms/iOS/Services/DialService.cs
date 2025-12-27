namespace NTS.Witness.Platforms.Services;

public class DialService : IDialService
{
    public void DialNumber(string phoneNumber)
    {
        var url = new Foundation.NSUrl($"tel:{phoneNumber}");
        if (UIKit.UIApplication.SharedApplication.CanOpenUrl(url))
        {
            UIKit.UIApplication.SharedApplication.OpenUrl(
                url,
                new Foundation.NSDictionary(),
                (success) =>
                {
                    // Optional: handle success/failure of dialer launch
                    Console.WriteLine($"Dialer opened: {success}");
                }
            );
        }
    }
}
