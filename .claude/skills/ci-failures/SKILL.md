---
name: ci-failures
description: Find out why Claudette's CI failed or why a test is flaky, and fix the cause rather than the symptom. Use when CI is red, a test fails only sometimes or on only one system, or when asked about failing or flaky tests.
---

# Fix a failing or flaky test

A test that fails only sometimes has a cause: a race, a wait on the wrong thing, a ceiling a slow runner outran, or a
difference between systems. Find which before changing anything. Never retry, skip or quarantine a test, and never add
a fixed delay (CLAUDE.md, "Tests").

## 1. Collect the failures

- List the CI workflow's failed runs (`ci.yml`; `package.yml` runs it too before a release). Cloud sessions use the
  GitHub tools; locally, `gh run list --workflow ci.yml --status failure`.
- A failed leg uploads `test-results-<os>`: its `.trx` files and any `*.received.txt`, kept for 7 days. Download and
  unzip each into a folder of its own and summarize it with `node .github/scripts/test-summary.mjs <folder>`.
- Older runs have only their logs. Ask for the failed jobs' logs alone, save them rather than reading them whole, and
  search them for `[FAIL]` and the `Error Message:` after it.
- Group the failures by test, with the system, the message and how long the test took (its `UnitTestResult`'s
  `duration`).

## 2. Rule out what isn't a flake

- `git log` the test and the code it covers since the failing commit: a later commit may already have fixed it.
- A test failing on all three systems at once is rarely a flake: a stale snapshot (the artifact has its
  `*.received.txt`) or a real regression. Fix it as one.
- A RealCli test that fails only inside a Claude Code session isn't CI's failure: there the real `claude` is the
  session's own, not the version `compat/surface.yaml` names (`lastTested`), and the session's `CLAUDE_*` variables
  pass through to it as user configuration.

## 3. Tell a slow runner from a bug

Each `UnitTestResult` has a `startTime` and a `duration`. Sort a run's results by `startTime`, and compare what ran
alongside the failure with the same tests in another run:

- **The runner stalled** when unrelated tests in the same minutes took 10 to 100 times as long too. If only the tests
  that write to disk slowed (SQLite, git, the library, atomic writes) and the ones that only compute didn't, it was the
  disk. The fix is the ceiling: the wait uses `Waiting.Timeout`.
- **It's a race or a wrong wait** when the failure came fast (a missing key after 60 ms, an assertion straight after a
  wait), or when only that test was slow. Read the code between the wait and the assertion.

## 4. Reproduce it

- Build Release, then run the one test in a loop from its xunit v3 executable, which starts faster than `dotnet test`:
  `tests/<Project>/bin/Release/net10.0/<Project> -method "<Namespace.Class.Method>" -noLogo`, counting the runs whose
  summary says `Failed: 0`.
- Load the machine as a busy runner is, with a busy loop per core (`sh -c 'while :; do :; done'`) started and stopped by
  a script of its own. Don't stop them with `pkill -f` on a pattern your own shell's command line also contains.
- A race that won't show can be proven by widening its window: a temporary `Thread.Sleep` where the gap is, with
  `#pragma warning disable RS0030` around it. Show the old code failing with it and the fix passing with the same sleep,
  then take the sleep out.

## 5. Fix the cause

In this order of preference:

1. **The order of the code.** Set what asynchronous work reads before anything can start that work, rather than
   counting on the UI thread to queue it (CLAUDE.md, "Rules that keep the code testable"). A race the tests see can be
   one people hit on a slow machine.
2. **What the test waits for.** The state the next step reads, not one that comes before it or is already true.
3. **The ceiling.** Real work is bounded by `Waiting.Timeout`, not a shorter time picked for the case.

A bug people could have run into, such as an error that hides the real one, gets its entry under **Fixed** in
CHANGELOG.md.

## 6. Prove it

- The new or fixed test fails without the fix and passes with it.
- The free suite passes several times in a row under load:
  `dotnet test Claudette.slnx -c Release --no-build --filter "Category!=Live&Category!=RealCli"`.
- The pull request says, for each failure, what it was, how it was reproduced and what shows it fixed.
