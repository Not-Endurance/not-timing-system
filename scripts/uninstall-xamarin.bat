del /F /Q "%USERPROFILE%\.dotnet"
del /F /Q "%APPDATA%\Xamarin"
del /F /Q "%USERPROFILE%\AppData\Local\Xamarin"
del /F /Q "C:\Program Files (x86)\MSBuild\Xamarin"

# Android
del /F /Q "%LOCALAPPDATA%\Android"
del /F /Q "%USERPROFILE%\.android"
del /F /Q "%PROGRAMDATA%\Microsoft\Android"
del /F /Q "C:\Program Files\Microsoft\Android"
del /F /Q "C:\Program Files\dotnet\packs\Microsoft.Android.Sdk.*"
del /F /Q "%USERPROFILE%\.gradle"
rd /s /q "%USERPROFILE%\.nuget\packages"
rd /s /q "%USERPROFILE%\.dotnet"

# Apple
del /F /Q "%LOCALAPPDATA%\Xamarin\iOS"
del /F /Q "%LOCALAPPDATA%\Xamarin\AppleSimulator"
del /F /Q "%LOCALAPPDATA%\Xamarin\mtbs"
del /F /Q "%USERPROFILE%\AppData\Local\Xamarin\MonoTouch"
del /F /Q "%USERPROFILE%\AppData\Local\Xamarin\iOS"
del /F /Q "%USERPROFILE%\.mono"
del /F /Q "C:\Program Files (x86)\MSBuild\Xamarin\iOS"