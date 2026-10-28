# Mutation test: reference

Reached from `SKILL.md` when a run is slow, a survivor looks equivalent, a catch looks suspect, or the report is due.

## Fast runs

A mutant costs one build and the filtered tests. The integration project of this repo takes about 90 s to build; a scratch project that links only the files under test took about 25 s.

- **Scratch project.** Create it inside the tree, so that the links are to the tree's files, and point the mutants at it with `project:`.

      <Project Sdk="Microsoft.NET.Sdk">
        <PropertyGroup>
          <TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
          <IsPackable>false</IsPackable><IsTestProject>true</IsTestProject>
        </PropertyGroup>
        <ItemGroup>
          <Compile Include="..\tests\NTS.Tests.Integration\Architecture\*.cs" LinkBase="Architecture" />
        </ItemGroup>
        <ItemGroup>
          <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
          <PackageReference Include="xunit" Version="2.5.3" />
          <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" PrivateAssets="all" />
        </ItemGroup>
        <ItemGroup><Using Include="Xunit" /></ItemGroup>
      </Project>

  It suits code and tests with few dependencies (rules, parsers, readers). Link every file the tests need, `ProjectReference` the library when the code lives in one.
- **Only the test project's own files change:** `test-command: rtk dotnet test {project} --no-restore -p:BuildProjectReferences=false -v:m --filter {tests}` skips the dependency graph.
- **A file a test reads while it runs** (a project file, a setting) needs no build: `--no-build`, after a baseline build from clean sources. A build left over from a source mutant gives false catches.
- **Workers.** Each worker has its own tree, which builds on its own, and its own results file; keep them well below the CPU count (four on 20 CPUs saturated the machine).

      mt tree create [SPEC] --run r1-w2
      mt run --run r1 --part 0/2
      mt run --run r1 --part 1/2 --root .tmp/mutation-test/r1-w2/tree --log results-2.jsonl

  After `tree sync` on one tree, sync them all and pass `--rebaseline`. Remove the extra tree with `mt tree remove --run r1-w2`.

## Equivalent and no-seam

Before a survivor is called equivalent, try to write the test that tells it apart: if you can, it is a gap. The ones that held up:

- two flags that share one row of a policy, so the Api answers alike for everybody
- a guard that only a race shows
- what a lower layer normalizes anyway (a trim, a case), which makes the check above it redundant: dead code, not a gap
- a rule that lives in two places, so that either alone shows nothing: mutate both together in one mutant, and a test then shows it
- an order that the store keeps anyway
- a change that gives the same value (`x < lo` for `x <= lo` where both return `lo`)

A mutant that no seam can tell apart is `no-seam`: say where a seam would be, and write no test at a seam nobody agreed.

## False catches

A `caught` counts when a test that should notice the mutant went red. Read `tests` and `reasons` in `results.jsonl` (or the report) and doubt the rest:

- the code did not compile: the runner makes it `invalid`, so it is not a catch
- a container failed to start, a timeout under load: `infra` and `timeout`, rerun alone
- a build left over from an earlier mutant (`--no-build`)
- a file an IDE wrote back into the tree
- a test that is red unmutated: the baseline stops the run

False survivors come from a filter that matches nothing (the runner calls that `invalid`, since dotnet exits 0 for it) or one that is too narrow (Triage, first step).

To switch a branch off without an "unreachable code" compile error, write `Environment.ProcessId < 0 && condition`.

## Report

    ## Mutation test: <the scope in words> (<range>)
    <the table of `mt report`>
    **Gaps closed:** <each new test: file, test name, the mutants it kills>
    **Explained:** <each disposition, one line>
    **Not mutated:** <changed code with no mutant, and why>
    **Caveats:** the mutants come from my reading of the diff, so my blind spots are theirs; gaps I predicted and closed before the first run were not found by the run.
    **Open:** <survivors left after three rounds, and what needs the owner>

Keep the numbers the report printed. List the new tests as uncommitted files.
