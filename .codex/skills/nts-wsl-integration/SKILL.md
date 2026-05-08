---
name: nts-wsl-integration
description: Set up and verify the NTS integration test environment in WSL. Use when Codex is asked to run, fix, or document NTS.Tests.Integration in WSL; install or repair Azure Functions Core Tools for this repo; create a WSL func shim; use Docker Desktop from WSL; start Azurite; or diagnose integration harness startup failures involving func, Docker, Azurite, MongoDB connection strings, or WSL localhost.
---

# NTS WSL Integration

## Core Rule

Read the repository `AGENTS.md` before making changes. If it forbids writes outside the working directory, ask the user to lift that rule before creating `~/.local/bin/func` or editing `~/.bashrc`.

## Use The Linux Func Path

Prefer Linux Azure Functions Core Tools under the repository `.tools/` directory. Do not rely on Windows `func.exe` for the integration harness: it can start the Functions host, but WSL may not reach the Windows loopback listener at `127.0.0.1:<port>`.

Use Docker Desktop's WSL CLI whenever Docker is needed:

```bash
/mnt/wsl/docker-desktop/cli-tools/usr/bin/docker
```

## Setup Workflow

From the repository root:

1. Verify the repo rules:

```bash
sed -n '1,180p' AGENTS.md
```

2. If home-directory writes are allowed or the user explicitly lifts the rule, run the setup script:

```bash
bash .codex/skills/nts-wsl-integration/scripts/setup_wsl_integration.sh
```

3. If home-directory writes are not allowed yet, install only repo-local tools first:

```bash
bash .codex/skills/nts-wsl-integration/scripts/setup_wsl_integration.sh --skip-shim --skip-bashrc
```

4. Verify without modifying files:

```bash
bash .codex/skills/nts-wsl-integration/scripts/setup_wsl_integration.sh --verify-only
```

5. Run the integration project with the expected output levels:

```bash
PATH="/mnt/wsl/docker-desktop/cli-tools/usr/bin:$HOME/.local/bin:$PATH" dotnet build tests/NTS.Tests.Integration/NTS.Tests.Integration.csproj -v:q
PATH="/mnt/wsl/docker-desktop/cli-tools/usr/bin:$HOME/.local/bin:$PATH" dotnet test tests/NTS.Tests.Integration/NTS.Tests.Integration.csproj -v:m
```

## Expected Installed Pieces

- `.tools/node`: local Linux Node runtime used for npm/extraction.
- `.tools/npm`: local npm prefix containing `azure-functions-core-tools`.
- `.tools/npm/node_modules/azure-functions-core-tools/bin/func`: Linux `func` binary.
- `~/.local/bin/func`: optional shim pointing at the repository-local Linux `func`.
- `~/.bashrc`: optional PATH line placing Docker Desktop's WSL CLI and `~/.local/bin` first.

`.tools/` is local machine state and should stay git-ignored.

## Common Failures

- `func: command not found`: create or repair the shim after user approval for home-directory writes.
- Docker reports `client version 1.43 is too old`: use `/mnt/wsl/docker-desktop/cli-tools/usr/bin/docker` instead of `/usr/bin/docker`.
- `MongoDB connection string is null`: usually caused by launching Windows `func.exe`; switch to Linux `func`.
- `Nexus HTTP did not become healthy` while Functions lists routes: often Windows `func.exe` loopback isolation; switch to Linux `func`.
- Azurite storage errors: start the `azurite` Docker container on ports `10000`, `10001`, and `10002`.

## Script Notes

The setup script downloads a large Functions Core Tools zip from Microsoft's CDN and extracts about 1.3 GB under `.tools/`. This can be slow on `/mnt/*` drives. Prefer checking whether `.tools/npm/node_modules/azure-functions-core-tools/bin/func --version` works before repeating the download.
