# Two test projects, and integration tests walk features through numbered stages

Status: proposed

The unit tests run in no workflow, the integration tests run only when someone starts them by hand, the two projects overlap, the folders do not follow `src/`, and a failure rarely says which scenario or stage failed. We decided that there are two test projects for the product, `NTS.Tests.Unit` and `NTS.Tests.Integration`, and one shared library, `nugets/Not.Test`, for generic test code, with its own test project, `nugets/Not.Test.Tests`. Unit tests cover the domain, and the application logic where it is judged necessary. Integration tests cover each feature as a journey through numbered stages, with scenarios for the ways a journey can go, so that a failure names the feature, journey, scenario and stage it stopped at.

## What follows from it

**Shared library.** `nugets/Not.Test` holds the generic pieces: the container fixture, the process runner, the deadline wait, `TestId`, the journey runner, the browser kit and the project-graph reader. It sits beside the other `Not.*` libraries, is packaged for future projects, and references no NTS project. It may take Playwright: the architecture rule that only tests take Playwright exempts it, and `Not.Test.Tests`, by name. Its own tests live in `nugets/Not.Test.Tests`, a separate project. Product-specific harness code stays in its test project's `Harness` folder: the Api factory, the seeds, the drivers and the outbox double.

**Unit tests.** They mirror `src/` and `nugets/`, need no host, database, network, browser or real clock, and are named by their method, so they carry no failure wrapper and no assertion messages. The architecture tests are unit tests.

**Integration tests.** A feature is one capability of the product, and its folder holds its tests. A journey is the path a feature takes from its entry point to its outcome, as numbered stages. A scenario is one case of a journey: every stage succeeds, the journey stops at one of them, or it runs under different rules, such as the FEI or the Regional ruleset. Each scenario is one test. For email login, the feature has one journey, "Sign in with an emailed code", with the stages 1 Login form, 2 Email code, 3 Enter code and 4 Receive cookie, and scenarios for success and for the refusals worth showing at a stage.

**Border.** An integration test doubles only what the product does not own: the clock and outbound email. The host, MongoDB, the live connection and the browser are real.

**Determinism.** No sleeps. A wait polls a condition until a deadline and names what it waited for. Ids come from `TestId`. The fake clock starts at the real instant, which is the one real-clock read a test may make. The banned APIs (clock reads, `Guid.NewGuid`, `Random`, `Thread.Sleep` and `Task.Delay`) fail the build in both test projects, through a rule in the analyzers repository.

**Robustness.** No automatic retries and no quarantine. A flaky test is made deterministic in the change that finds it. A change lands only after three consecutive full runs of the integration project pass on one machine.

**Containers.** One MongoDB container per test run (`MongoContainer`), shared by every class, with each class in a database of its own (`MongoLease`), which the class drops when it ends. Four classes run at a time, each holding a place of `MongoLease`, because the Visual Studio adapter does not apply the xUnit thread limit. Chromium runs at most two at a time (`PasskeyBrowserFixture`).

**CI.** Pull requests run the unit tests, which include the architecture tests, and fail on them. Integration tests run only from the manual workflow.

## Considered options

- **Mirroring `src/` in the integration project.** Rejected. What a person can do is tested where the journey runs, not where the code sits.
- **A container per class, as today.** Rejected. About 133 containers start in each run.
- **Automatic retries.** Rejected. A retry hides the failure it should report.
- **One integration test per rule** (expiry, attempts, cooldown). Rejected. A rule that a stage can show becomes a scenario of its journey, and a rule that is pure logic gets a unit test.

## Consequences

AGENTS.md rules 4 to 7 are replaced by a pointer to this record, and the `Temporary` namespace and every reference to it are removed. The parked scenarios stay out of the build until the owner approves each one (AGENTS.md rule 4). Each project's banned-API sweep lands with the change that turns its rule on: units first (#666), then integration (#667).
