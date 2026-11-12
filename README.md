# Not a Timing System (NTS)
Integrated app to create, configure and run your Endurance category equestrian sport events. NTS is the improved version of [EMS](https://github.com/Not-Endurance/endurance-management-system) which was started in 2018 on WPF

# Running the app
There are a couple of prerequisites related to MAUI Hybrid and NoTiming API

## Dotnet
Dotnet 8 or later

## MAUI
1. Open VisualStudio Installer and download MAUI tools (Modify, tick **.NET Multi-platform Application UI development** and install)
2. In terminal scope to [src-v2](https://github.com/Not-Endurance/not-timing-system/tree/develop/src-v2) and run `dotnet workload restore` - this will install necessary workloads based on the solution

## WebView2
MAUI Blazor hybrid apps use MS's WebView2 in order to run natively. This might not be necessary, I don't  recall if VS installer handles it or you need to download manually. If you see a popup regarding WebView follow the instructions or download manually [here](https://developer.microsoft.com/en-us/microsoft-edge/webview2/?form=MA13LH). You may need ro reboot after installation.

## Debug
1. Start the NoTiming API:
   ```
   // Execute in project root
   docker compose -f docker/compose.yaml up -d
   ```
3. Startup `NoTiming.Judge.MAUI`

### Publish and run release build
1. Using a shell-based terminal (git bash for example) scope to [Judge MAUI directory](https://github.com/Not-Endurance/not-timing-system/tree/develop/src-v2/Judge/NoTiming.Judge.MAUI) and run `./publish-windows.sh` - this will run `dotnet publish` and open the bin directory when complete.
2.  Find and run `NoTiming.Judge.MAUI.exe` in order to start the application.

### Diagrams
- A reasonably-updated [Domain Model diagram](https://github.com/Not-Endurance/not-timing-system/blob/develop/diagrams/NTS%20v2%20Domain%20Model.drawio.png)
- [Appflow diagram](https://github.com/Not-Endurance/not-timing-system/blob/develop/diagrams/NTS%20v2%20Appflow.drawio.png)

### Project structure
1. [src](https://github.com/Not-Endurance/not-timing-system/tree/develop/src) contains EMS and is already extracted in a separate repository and will be removed from here soon.
2. [src-v2](https://github.com/Not-Endurance/not-timing-system/tree/develop/src) contains the NTS code and will be renamed to src soon
1. [_EMS](https://github.com/Not-Endurance/not-timing-system/tree/develop/src/Compatibility.EMS) are objects from EMS necessary in order to achieve backwards compatibility with the mobile app [EMS.Witness](https://github.com/Not-Endurance/endurance-management-system/tree/release-witness-v5/src/Witness/EMS.Witness) You can see more of it's usage in [NoTiming.Judge/ACL](https://github.com/Not-Endurance/not-timing-system/tree/develop/src/Judge/NoTiming.Judge/ACL) which stands for anti-corruption layer and essentially converts to and from EMS state
2. [Not](https://github.com/Not-Endurance/not-timing-system/tree/develop/src/Not) is a future library where I stuff all of the infrastructure and components in order to reuse them for future projects. They are completely stripped from business logic and are desidned to be configurable and exensible
3. [Judge](https://github.com/Not-Endurance/not-timing-system/tree/develop/src/Judge) contains the main app with the following separation:
  - `NoTiming.Judge` contains the **Behinds** (from code-behind) which invoke the Domain Model and fascilitate the operations
  - `NoTiming.Judge.MAUI` is the MAUI startup project
  - `NoTiming.Judge.Blazor` contains the blazor components. These are segregated because once we reach a certain scale that we can afford to run servers it will be simpler to use a web-based blazor app, rather than integrated.
4. [Relay](https://github.com/Not-Endurance/not-timing-system/tree/develop/src/Relay) is a separet ASP.NET app kickstarted by Judge using `Process.Start`. It serves as SignalR relay between Judge and Witness
5. [Nexus](https://github.com/Not-Endurance/not-timing-system/tree/develop/src/Nexus) is Azure Functions API that stores event archives and other data long term. 
4. `NoTiming.Domain*` - contains the domain model for NTS. It defines 3 separate boundaries:
  - `Setup` - create and configure an event
  - `Core` - service the event
  - `Watcher` - is not yet implemented and is supposed to encompass the timing modules - RFID tags, AI cameras, Ui app etc
5. `NTS.Persistence` - handles state and storage. As of now the "database" is a simple json file, as the day-to-day nature of the integrated app does not require an actual database at this point.

# Database migrations
MongoDB schema and data changes live in [database](database/README.md) and run with [migrate-mongo](https://github.com/seppevs/migrate-mongo). Each migration is a script in `database/migrations` that exports `up(db)` and `down(db)`.

- The CD workflows run `npm run migrate:up` in a `migrate-mongo` job before the web app deploys. The job runs in the `staging` or `production` GitHub environment and reads the `MONGO_CONNECTION_STRING` secret from it.
- Applied migrations are recorded in the `changelog` collection, so running `migrate:up` again is safe.
- To add one, run `npx --prefix database migrate-mongo create <short-description>` and ship it with the change that needs it.
- Don't point local runs at production unless you mean to change production.
