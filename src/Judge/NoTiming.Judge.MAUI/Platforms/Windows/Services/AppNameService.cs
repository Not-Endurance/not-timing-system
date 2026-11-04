using NoTiming.Judge.MAUI.Platforms.Services;

namespace NoTiming.Judge.MAUI.Platforms.Windows.Services;

public class WindowsProcessService : IMauiProcessService
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
