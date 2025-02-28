# Not a Timing System (NTS)
Integrated app to create, configure and run your Endurance category equestrian sport events. NTS is the improved version of [EMS](https://github.com/Not-Endurance/endurance-management-system) which was started in 2018 on WPF

# Running the app

## Dotnet
Dotnet 8 or later

## MAUI
1. Open VisualStudio Installer and download MAUI tools (Modify, tick **.NET Multi-platform Application UI development** and install)
2. In terminal scope to [src-v2](https://github.com/Not-Endurance/not-timing-system/tree/develop/src-v2) and run `dotnet workload restore` - this will install necessary workloads based on the solution

## WebView2
MAUI Blazor hybrid apps use MS's WebView2 in order to run natively. This might not be necessary, I don't  recall if VS installer handles it or you need to download manually. If you see a popup regarding WebView follow the instructions or download manually [here](https://developer.microsoft.com/en-us/microsoft-edge/webview2/?form=MA13LH). You may need ro reboot after installation.

### Debug
1. Configure `StaticOptions` - [static-options.json](https://github.com/Not-Endurance/not-timing-system/blob/develop/src-v2/Judge/NTS.Judge/Resources/config/static-options.json) is something like an ad hoc appsettings.json. It's packaged in the build process, but when debugging (DEBUG constant is defined) it's sourced from `C:\tmp\nts\Resources\config`. It's mostly necessary in order to provision Country dropdown lists in a few forms
2. Startup `NTS.Judge.MAUI`

### Publish and run release build
1. Using a shell-based terminal (git bash for example) scope to [Judge MAUI directory](https://github.com/Not-Endurance/not-timing-system/tree/develop/src-v2/Judge/NTS.Judge.MAUI) and run `./publish-windows.sh` - this will run `dotnet publish` and open the bin directory when complete.
2.  Find and run `NTS.Judge.MAUI.exe` in order to start the application.

### Diagrams
- A reasonably-updated [Domain Model diagram](https://github.com/Not-Endurance/not-timing-system/blob/develop/diagrams/NTS%20v2%20Domain%20Model.drawio.png)
- [Appflow diagram](https://github.com/Not-Endurance/not-timing-system/blob/develop/diagrams/NTS%20v2%20Appflow.drawio.png)

### Project structure
1. [src](https://github.com/Not-Endurance/not-timing-system/tree/develop/src) contains EMS and is already extracted in a separate repository and will be removed from here soon.
2. [src-v2](https://github.com/Not-Endurance/not-timing-system/tree/develop/src-v2) contains the NTS code and will be renamed to src soon
1. [_EMS](https://github.com/Not-Endurance/not-timing-system/tree/develop/src-v2/Compatibility.EMS) are objects from EMS necessary in order to achieve backwards compatibility with the mobile app [EMS.Witness](https://github.com/Not-Endurance/endurance-management-system/tree/release-witness-v5/src/Witness/EMS.Witness) You can see more of it's usage in [NTS.Judge/ACL](https://github.com/Not-Endurance/not-timing-system/tree/develop/src-v2/Judge/NTS.Judge/ACL) which stands for anti-corruption layer and essentially converts to and from EMS state
2. [Not](https://github.com/Not-Endurance/not-timing-system/tree/develop/src-v2/Not) is a future library where I stuff all of the infrastructure and components in order to reuse them for future projects. They are completely stripped from business logic and are desidned to be configurable and exensible
3. [Judge](https://github.com/Not-Endurance/not-timing-system/tree/develop/src-v2/Judge) contains the main app with the following separation:
  - `NTS.Judge` contains the **Behinds** (from code-behind) which invoke the Domain Model and fascilitate the operations
  - `NTS.Judge.MAUI` is the MAUI startup project
  - `NTS.Judge.Blazor` contains the blazor components. These are segregated because once we reach a certain scale that we can afford to run servers it will be simpler to use a web-based blazor app, rather than integrated.
  - `NTS.Relay` is a separet ASP.NET app spinned alongside the integrated, which fascilitates the connection with the mobile Witness apps
4. `NTS.Domain*` - contains the domain model for NTS. It defines 3 separate boundaries:
  - `Setup` - create and configure an event
  - `Core` - service the event
  - `Watcher` - is not yet implemented and is supposed to encompass the timing modules - RFID tags, AI cameras, Witness app etc
5. `NTS.Persistence` - handles state and storage. As of now the "database" is a simple json file, as the day-to-day nature of the integrated app does not require an actual database at this point.
