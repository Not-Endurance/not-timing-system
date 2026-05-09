## Rules
1. Don't execute commands that would create, delete or modify files outside of this working directory. Exceptions of this rule are commands which could cause side effects scoped to the utility that owns the command:
- dotnet msbuild/nugget/workload or similar caches and or build artifacts
- docker commands which would modify containers, volumes, images or similar
2. Always dotnet `build`, `restore` with silent output `-v:q` unless instructed otherwise
3. Run dotnet `test` with minimal output `-v:m`

## WSL
1. When docker cli is necessary - run from /mnt/wsl/docker-desktop/cli-tools/usr/bin/docker
2. When func is not resolved - ask the user to lift Rule #1 so you can set it up
3. Ignore MAUI builds in WSL