# Issue tracker: GitHub

Issues and specs for this repo live as GitHub issues. Use the `gh` CLI for all operations.

## Conventions

- **Create an issue**: `gh issue create --title "..." --body "..." --label "source:agent"`. Use a heredoc for multi-line bodies.
- **Always label agent-created issues `source:agent`.** This applies to every issue you open, whatever the skill or workflow — it is what lets a human tell agent-filed issues from their own at a glance. Add topic labels (`Bug`, `Testing`, …) alongside it, never instead of it. Never apply it to an issue a human opened.
- **Label every parent issue `spec`.** A parent is an issue that has, or is meant to have, sub-issues: a spec or an umbrella (a wayfinder map has its own label). Apply `spec` alongside `source:agent` and the topic labels so specs can be selected by label, and never put it on a sub-issue. If the repository has no `spec` label yet, create it first: `gh label create spec --description "Parent issue that specifies a piece of work"`.
- **Read an issue**: `gh issue view <number> --comments`, filtering comments by `jq` and also fetching labels.
- **List issues**: `gh issue list --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'` with appropriate `--label` and `--state` filters.
- **Comment on an issue**: `gh issue comment <number> --body "..."`
- **Apply / remove labels**: `gh issue edit <number> --add-label "..."` / `--remove-label "..."`
- **Close**: `gh issue close <number> --comment "..."`

Infer the repo from `git remote -v` — `gh` does this automatically when run inside a clone.

## Issue body structure

Every issue an agent creates (spec, ticket, bug, chore) uses these three sections in this order, whichever skill publishes it. A skill's own template (Problem Statement / Solution / User Stories, What to build / Acceptance criteria, ...) is not dropped: it becomes the content of `## Details`.

1. `## Overview` - a few sentences: the problem and the solution.
2. `## Verification` - a `- [ ]` checklist of what a human checks on Staging once this is deployed. Observable behaviour only (what to open or do, and what should be seen), not test names. Write `N/A - not deployable (<reason>)` when nothing can be observed on Staging.
3. `## Details` - technical instructions plus any other sections the work needs (user stories, implementation and testing decisions, acceptance criteria, blocked-by, out of scope).

Wayfinder maps and their decision tickets keep their own structure.

## Closing issues

Implemented work is closed by merging a PR into the repository's default branch. Every commit and PR that implements an issue carries a closing keyword:

- Commit message: a `Closes #<n>` line before the Co-Authored-By trailer.
- PR description: `Closes #<n>` for each issue the PR completes. Repeat the keyword per issue: `Closes #1, closes #2`. `Closes #1, #2` only closes the first.
- Only issues the work completes, never follow-up tickets mentioned in the body. Close a parent spec only when the PR completes its last ticket.
- GitHub acts on the keyword only when the PR merges into the default branch. A PR into another branch (e.g. a stacked branch) closes nothing, so repeat the keywords in the PR that lands on the default branch.
- If the work has no issue, omit the keyword.

## Pull requests as a triage surface

**PRs as a request surface: no.** _(Set to `yes` if this repo treats external PRs as feature requests; `/triage` reads this flag.)_

When set to `yes`, PRs run through the same labels and states as issues, using the `gh pr` equivalents:

- **Read a PR**: `gh pr view <number> --comments` and `gh pr diff <number>` for the diff.
- **List external PRs for triage**: `gh pr list --state open --json number,title,body,labels,author,authorAssociation,comments` then keep only `authorAssociation` of `CONTRIBUTOR`, `FIRST_TIME_CONTRIBUTOR`, or `NONE` (drop `OWNER`/`MEMBER`/`COLLABORATOR`).
- **Comment / label / close**: `gh pr comment`, `gh pr edit --add-label`/`--remove-label`, `gh pr close`.

GitHub shares one number space across issues and PRs, so a bare `#42` may be either — resolve with `gh pr view 42` and fall back to `gh issue view 42`.

## When a skill says "publish to the issue tracker"

Create a GitHub issue, labelled `source:agent`.

## When a skill says "fetch the relevant ticket"

Run `gh issue view <number> --comments`.

## Wayfinding operations

Used by `/wayfinder`. The **map** is a single issue with **child** issues as tickets.

- **Map**: a single issue labelled `wayfinder:map`, holding the Notes / Decisions-so-far / Fog body. `gh issue create --label wayfinder:map --label source:agent`.
- **Child ticket**: an issue linked to the map as a GitHub sub-issue (`gh api` on the sub-issues endpoint). Where sub-issues aren't enabled, add the child to a task list in the map body and put `Part of #<map>` at the top of the child body. Labels: `wayfinder:<type>` (`research`/`prototype`/`grilling`/`task`). Once claimed, the ticket is assigned to the driving dev.
- **Blocking**: GitHub's **native issue dependencies** — the canonical, UI-visible representation. Add an edge with `gh api --method POST repos/<owner>/<repo>/issues/<child>/dependencies/blocked_by -F issue_id=<blocker-db-id>`, where `<blocker-db-id>` is the blocker's numeric **database id** (`gh api repos/<owner>/<repo>/issues/<n> --jq .id`, _not_ the `#number` or `node_id`). GitHub reports `issue_dependencies_summary.blocked_by` (open blockers only — the live gate). Where dependencies aren't available, fall back to a `Blocked by: #<n>, #<n>` line at the top of the child body. A ticket is unblocked when every blocker is closed.
- **Frontier query**: list the map's open children (`gh issue list --state open`, scoped to the map's sub-issues / task list), drop any with an open blocker (`issue_dependencies_summary.blocked_by > 0`, or an open issue in the `Blocked by` line) or an assignee; first in map order wins.
- **Claim**: `gh issue edit <n> --add-assignee @me` — the session's first write.
- **Resolve**: `gh issue comment <n> --body "<answer>"`, then `gh issue close <n>`, then append a context pointer (gist + link) to the map's Decisions-so-far.
