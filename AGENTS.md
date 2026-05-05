## Restrictions
1. Don't execute commands that would create, delete or modify files outside of this working directory. Exceptions of this rule are commands which could cause side effects scoped to the utility that owns the command:
- dotnet msbuild/nugget or similar caches and or build artifacts
- docker commands which would modify containers, volumes, images or similar
2. When always dotnet `build` and `restore` with silent output `-v:q`