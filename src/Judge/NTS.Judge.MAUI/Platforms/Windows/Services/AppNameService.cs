using NTS.Judge.MAUI.Platforms.Services;

namespace NTS.Judge.MAUI.Platforms.Windows.Services;

public class AppNameService : IAppName
{
    public void SetAppName(string appName)
    {
        Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping(
           nameof(IWindow),
           (handler, view) =>
           {
               var nativeWindow = handler.PlatformView;
               nativeWindow.Title = appName;
           }
       );
    }
}
