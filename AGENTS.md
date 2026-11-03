## Context
- Primary sln file is src/not-timing-system.sln (production code only)
- Test solution is tests/NoTiming.Tests.sln (unit test projects, Integration tests, Not.Tests)

## Rules
1. Don't execute commands that would create, delete or modify files outside of this working directory. Exceptions of this rule are commands which could cause side effects scoped to the utility that owns the command:
- dotnet msbuild/nugget/workload or similar caches and or build artifacts
- docker commands which would modify containers, volumes, images or similar
2. Always dotnet `build`, `restore` with silent output `-v:q` unless instructed otherwise. Don't modify worklfows to use queit mode. This applies only for localhost verification
3. Run dotnet `test` with minimal output `-v:m`.
4. Verify each feature with a successful NTS.Tests.Integration run. If the integration tests fail, don't change them directly. Instead report why they are failing.
5. Changes and features should be represented in the Integration tests if reasonably possible.
6. Domain logic is unit tested in the test project that mirrors its source project (for example NTS.Domain.Core.Tests and NTS.Domain.Setup.Tests). See Testing below.
7. Don't concern yourself with code formatting. CSharpier formats code via CI, so don't run or check formatting locally.

## Verification
- After each change, build and run the unit test project that mirrors the changed source project, for example `dotnet build tests/NoTiming.Tests.sln -v:q` followed by `dotnet test tests/NTS.Domain.Core.Tests/NTS.Domain.Core.Tests.csproj -v:m`.
- Verify each feature with a successful run of `dotnet test tests/NTS.Tests.Integration/NTS.Tests.Integration.csproj -v:m`. This needs Docker.

## Testing
Conventions for unit tests (Integration tests are not subject to these rules):
- Test projects: one per source project, named `<SourceProject>.Tests` and placed in `tests/`. Example: `src/NTS.Application` is tested by `tests/NTS.Application.Tests`.
- Test class: named `<TestedClass>Tests`, where `<TestedClass>` is the class under test.
- Test location: mirror the source directory. A class at `src/Domain/NTS.Domain.Core/Objects/Startlists/Startlist.cs` is tested by `tests/NTS.Domain.Core.Tests/Objects/Startlists/StartlistTests.cs`. Namespaces follow the folder.
- Test method: `<Method>_When<Condition>_<Result>`, for example `Create_WhenInvalid_ShouldReturnError`. The result starts with `Should`. For constructors, `<Method>` is `Constructor`.
- One behaviour per test. Several `Assert` calls are fine when they check one atomic behaviour. If a test checks two separate behaviours, split it into two tests.
- Helpers and fakes used by one test class are private and nested in that class. Helpers or fakes shared by several test classes go in a `Harness/` directory at the lowest directory level that contains all the classes using them.

## TDD (tdd skill)
When using the `tdd` skill in this repository, follow these rules on top of the skill's defaults:
- Write the failing test first in its mirrored location, using the naming rules above.
- Run only the affected test project during the red and green steps. Run the solution-level build before reporting completion.
- Write one test per behaviour. Add a test for every new branch or validation rule.
- Test through the public API of the class. Don't test private methods or add test-only members to production code.
- Use fixed timestamps and values in new tests so results don't depend on the clock.
- Put helpers and fakes in the test class or `Harness/`, following the Testing section.
- Don't create temporary test files or folders. Every test lives in its final location.

## WSL
1. When docker cli is necessary - run from /mnt/wsl/docker-desktop/cli-tools/usr/bin/docker
2. When func is not resolved - ask the user to lift Rule #1 so you can set it up
3. Ignore MAUI builds in WSL

## Agent skills

### Issue tracker

GitHub Issues in `Not-Endurance/not-timing-system`, managed with the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Domain docs

Single-context: one `GLOSSARY.md` and `docs/adr/` at the repo root, created lazily. See `docs/agents/domain.md`.
