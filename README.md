# Not a Timing System (NTS)
Integrated app to create, configure and run your Endurance category equestrian sport events. NTS is the improved version of [EMS](https://github.com/Not-Endurance/endurance-management-system) which was started in 2018 on WPF

# Running the app
NoTiming is one web platform. `NoTiming.Api`, an ASP.NET Core host, serves `NoTiming.Ui`, a Blazor WebAssembly app, and the live hub on one origin (ADR-0011). It is the only server: the Azure Functions API and the Static Web App that served the Ui retired with it.

## Prerequisites
- .NET 10 SDK. Every project targets .NET 10.
- Docker, for MongoDB, where the Api keeps everything.

## Debug
1. Start MongoDB (execute in the project root):
   ```
   docker compose -f docker/compose.yaml up -d mongodb
   ```
   The same file runs the Aspire dashboard (`aspire-dashboard`, http://localhost:18888, the telemetry of the Api) and the Api in a container (`nts-api`, on http://localhost:11337, so start it instead of step 2 and not with it).
2. Run the Api, which serves the Ui:
   ```
   dotnet run --project src/NoTiming.Api --launch-profile Development
   ```
3. Open http://localhost:11337
4. To sign in, open `/sign-in`. In Development the Api has no mailbox to send to: it prints the email, with its six-digit code, in the log of `dotnet run`. The session cookie is Secure; Chrome and Firefox keep it on `http://localhost`, and for Safari use https://localhost:61380.

### Run it against another database, and sign in as somebody
A Debug build of the Api, in Development, reads the database named by `MONGO_CONNECTION_STRING` (`mongodb://localhost:27017` in `appsettings.Development.json`) and can sign you in as an account without a code. That is for the developer who runs the whole platform on their machine, on the container or on a hosted staging database. What belongs to one developer is kept in their user-secrets, which live in their profile and never in the repository:
1. Point the Api at the database:
   ```
   dotnet user-secrets set MONGO_CONNECTION_STRING "<connection string>" --project src/NoTiming.Api
   ```
2. Say which environment the database is, once. The route works only on a database that says it is Staging or Development: Production, no marker and anything else are refused, so a connection string that points at the wrong database cannot be signed in to by mistake (`seed-staging` and `migrate-tenants --apply` mark the databases they prepare, see `tools/NTS.Tools/README.md`):
   ```
   dotnet run --project tools/NTS.Tools -- mark-environment --connection-string <connection string> --environment Development --apply
   ```
3. Name the accounts that may be signed in as. They have to exist already: register at `/sign-in`, or make some with `seed-staging`:
   ```
   dotnet user-secrets set "Dev:SignInAs:AllowList:0" "you@example.org" --project src/NoTiming.Api
   ```
4. Run the Api as above and open http://localhost:11337/dev/sign-in-as, which lists those accounts. Clicking one signs you in as it: the session is the one a sign in makes, the address stays as unconfirmed as it was, and no invitation is taken, because signing in as somebody proves nothing about them.

The route and the page exist in a Debug build only (`dotnet run` builds one; the Release build that is published does not contain the code, and a test over a Release build says so), only in the Development environment, only when the allow-list names somebody, and only for a request from the machine itself: from a loopback address, to `localhost`, `127.0.0.1` or `[::1]`, and not through a proxy. A production database is for a read-only credential and nothing else, and never for a connection string that can write. The integration tests ignore your user-secrets, so what you set here does not change them.

## Publish
`dotnet publish src/NoTiming.Api -c Release` also publishes the Ui and ships it as the Api's `wwwroot`. Pass `-p:WasmApplicationEnvironmentName=Staging` (or `Production`): .NET 10 bakes the Blazor environment into the build, and without it the Ui loads the Production settings.

`.github/workflows/nts-cd.yaml` does that and deploys the result to the one App Service of the environment: staging from `develop`, production from `master`.

### Diagrams
- A reasonably-updated [Domain Model diagram](https://github.com/Not-Endurance/not-timing-system/blob/develop/diagrams/NTS%20v2%20Domain%20Model.drawio.png)
- [Appflow diagram](https://github.com/Not-Endurance/not-timing-system/blob/develop/diagrams/NTS%20v2%20Appflow.drawio.png)

### Project structure
Read [CONTEXT.md](CONTEXT.md) for the language of the domain and `docs/adr` for the decisions behind the layout.
- `src/NoTiming.Api` is the host, and the server: the JSON:API routes, the live hub, the sign-in pages, health, security headers and the Ui. What the application layer and the storage once were are folders of it (`Features`, `Hosting`), with the MongoDB wiring.
- `src/NoTiming.Ui` is the Blazor WebAssembly app: the pages, layout, view services and the REST repositories. It never references the Api, which references it.
- `src/NoTiming.Console` and `src/NTS.Blazor` are the Razor libraries of the Ui: the pages of the Console (set an Event up and run it) and the components they share.
- `src/Domain` holds the domain model in three boundaries: `NTS.Domain` (shared), `NTS.Domain.Setup` (create and configure an Event) and `NTS.Domain.Core` (run the Event). It references no infrastructure.
- `src/NTS.Contracts` holds the models and interfaces the Api and the Ui share. `src/NTS` holds the localization.
- `nugets` holds the `Not.*` libraries: generic infrastructure and components, free of business logic. `Not.Identity` is ASP.NET Core Identity over the existing user documents, the server-side session and the one-time codes (ADR-0002).
- `tools/NTS.Tools` is the command line of the developer: seeding, the data migrations and the checks of a database (see `tools/NTS.Tools/README.md`).
- `tests` holds the unit tests and the integration tests (see `tests/NTS.Tests.Integration/README.md`). The layering above is checked from the project files by the tests in `tests/NTS.Tests.Integration/Architecture`: the Ui never references the Api, the domain references no infrastructure, nothing references Azure Functions, every project targets .NET 10, and only the tests take Playwright (printing is the browser's).
