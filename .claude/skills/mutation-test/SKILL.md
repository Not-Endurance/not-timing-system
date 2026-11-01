---
name: mutation-test
description: Mutation-test a change: break its code on purpose, in a throwaway worktree, until the tests catch every mutant, then report. Use when asked to mutation-test or to check that the tests would notice bugs in a commit, a range or a described change (default: the latest commit)
---

# Mutation test

A **mutant** is the changed code with one deliberate bug. A test run that stays green is a **survivor**: the tests would not notice that bug. Iterate until every mutant is **caught** (a test goes red) or explained, then report.

`scripts/mutation.py` does the git, the running and the bookkeeping; you do the judgment: which mutants, which tests, what a survivor means. Below, `mt` is `python -X utf8 -I .claude/skills/mutation-test/scripts/mutation.py`. `mt --help` holds the mutants file format, and nothing else does.

## 1. Scope

The uncommitted changes (`--working-tree`), if such exist, otherwise the latest commit, unless the user names a commit, a range (`A..B`), `--last N`, a branch (`develop..HEAD`) or describes the change in words: find its commits with `git log --grep` or `-S`, and say the range you chose in one line.

    mt scope --code [SPEC]

Mutate the changed lines of code files. Tests, docs, config, deletions and files that only moved have nothing to mutate.

Done when every changed code file is on your work list or has a reason to be skipped.

## 2. Isolate

    mt tree create [SPEC] --run r1

Mutants live only in that worktree (`.tmp/mutation-test/r1/tree`). The working tree only ever receives new tests.

## 3. Write the mutants

Write `.tmp/mutation-test/r1/mutants.txt`. For each changed hunk ask what the smallest wrong version of each line would be, and write that:

- a condition: inverted, one side of `&&` or `||` dropped, `All` for `Any`, a boundary moved by one
- a list or constant: one entry removed, a literal changed
- a statement: the call deleted, a value made empty or default, two variables swapped
- a guard: a throw or early return that never fires, or always fires
- normalization: case-insensitive made case-sensitive, a trim or a separator dropped
- a message, status or code that clients branch on

Give each mutant the tests that should notice it (`tests:`): the tests of the same commit, then the tests that name the class. Then predict: which test goes red for each mutant? Where none would, write that test first (Rules) and `mt tree sync --run r1`.

Done when `mt check --run r1` prints `ok` and either lists no changed code that no mutant touches, or each item has a reason for the report.

## 4. Run

    mt run --run r1

In the background; wait for its completion notification. It measures the baseline first (every tests filter, unmutated, must be green and non-empty), then every mutant. Each ends `caught`, `survived`, `invalid` (did not compile, old text not found, no test ran), `infra` (retried) or `timeout`. When a run is slow (over 30 s a mutant) or you want workers: [REFERENCE.md](REFERENCE.md#fast-runs).

Done when every mutant is caught or survived. Fix an invalid mutant or its filter and rerun it with `--only NAME`; rerun an infra or timeout result alone.

## 5. Triage

Confirm each survivor under the widest filter you can afford, the whole test project: a narrow filter may have missed the test that catches it. A survivor is then one of:

- a **gap**: write a new test that fails on this mutant, `mt tree sync --run r1`, rerun it with `--only`
- **dead code**: say so in the report and propose removing it
- **equivalent**: no observable difference; one sentence of why (try to write the distinguishing test first; [REFERENCE.md](REFERENCE.md#equivalent-and-no-seam))
- **no seam**: the behavior shows at no agreed seam; say where a seam would be

Write the last three in `.tmp/mutation-test/r1/dispositions.txt`, one per line: `NAME: equivalent|no-seam|dead-code reason`. A `caught` whose failing test is not one that should notice the mutant is suspect: [REFERENCE.md](REFERENCE.md#false-catches).

Done when every mutant is caught or has a disposition. Three rounds of new tests per area is the limit; report the rest as open.

## 6. Report

    mt report --run r1

Answer with the report in the shape of [REFERENCE.md](REFERENCE.md#report). Post it as a comment only when asked.

## 7. Clean up

    mt tree remove --run r1

The new tests stay in the working tree, uncommitted, and the report lists them.

## Rules

- Tests you add are new tests, at seams already agreed (the `tdd` skill). An existing test changes only with the owner's OK (AGENTS.md rule 4).
- Commit, push and comment on issues when the user asks.
- Temp files live in `.tmp/mutation-test/` (AGENTS.md rule 10), removed in step 7.

## This repo

Header of `mutants.txt`, one project per mutant (`project:` overrides the default):

    test-command: rtk dotnet test {project} -v:m --filter {tests}
    project: tests/NTS.Tests.Unit/NTS.Tests.Unit.csproj

- Filters look like `FullyQualifiedName~ClassName|FullyQualifiedName~OtherClass`.
- Domain logic is held by unit tests, routes, stores and the Ui by `tests/NTS.Tests.Integration/NTS.Tests.Integration.csproj` (AGENTS.md rules 5 and 6). The integration project needs Docker; its browser classes need `NTS_INTEGRATION_PLAYWRIGHT_BROWSERS_PATH` set to the main tree's `.tmp/ms-playwright` before `mt run`.
- The first run in a fresh tree restores and builds everything, minutes of it; the baseline's own timeout covers that.
- A container that exits with code 48 is a known flake; the runner retries it as `infra`.
