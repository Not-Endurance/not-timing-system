# NTS Warp

Warp hosts three things on one port:

- the REST API under `/api`
- the SignalR hubs under `/event-bus/judge` and `/event-bus/witness`
- the Witness UI under `/ui/` (the root redirects there)

## Run locally

Visual Studio, Rider, or `dotnet run` can start these launch profiles from `Properties/launchSettings.json`:

| Profile       | Port  | Mongo connection string comes from             |
| ------------- | ----- | ---------------------------------------------- |
| `Development` | 11337 | environment variable (`mongodb://localhost:27017/nts`) |
| `Staging`     | 11338 | user secrets (`nts-warp-staging`)              |
| `Production`  | 11339 | user secrets (`nts-warp-production`)           |

Start Mongo for `Development` from the repository root:

```bash
docker compose -f docker/nexus.yaml up mongodb
```

`Development` also serves the Witness UI from a Debug build copy, and opens it in your browser once Warp is listening.

## Connection string secrets

The `Staging` and `Production` profiles read `MONGO_CONNECTION_STRING` from user secrets. Each environment has its own secret id, `nts-warp-` followed by the lowercased environment name:

```bash
dotnet user-secrets set --id nts-warp-staging MONGO_CONNECTION_STRING "<staging connection string>"
dotnet user-secrets set --id nts-warp-production MONGO_CONNECTION_STRING "<production connection string>"

dotnet user-secrets list --id nts-warp-staging
dotnet user-secrets remove MONGO_CONNECTION_STRING --id nts-warp-staging
```

The commands work from any directory. The secrets are stored per machine, in `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json` on Windows and `~/.microsoft/usersecrets/<id>/secrets.json` elsewhere. Do not commit connection strings to the repository.

The profiles set `IS_LOCALHOST=true`, which loads these secrets. Deployed services don't set it, so they never read secrets from a developer's machine. If the secret is missing, Warp exits at startup with `MongoDB connection string is missing`.
