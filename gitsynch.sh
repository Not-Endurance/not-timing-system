#!/usr/bin/env bash
set -euo pipefail

MASTER_BRANCH="master"
DEVELOP_BRANCH="develop"

run() {
    printf '\n==> %s\n' "$*"
    "$@"
}

require_git_repository() {
    if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
        echo "This script must be run from inside a git repository." >&2
        exit 1
    fi
}

require_clean_worktree() {
    if [[ -n "$(git status --porcelain)" ]]; then
        echo "Cannot continue because the worktree has uncommitted changes:" >&2
        git status --short >&2
        exit 1
    fi
}

require_branch() {
    local branch
    branch="$(git branch --show-current)"

    if [[ -z "$branch" ]]; then
        echo "Cannot continue from a detached HEAD." >&2
        exit 1
    fi

    printf '%s' "$branch"
}

main() {
    require_git_repository
    require_clean_worktree

    local stored_branch
    stored_branch="$(require_branch)"

    echo "Stored branch: ${stored_branch}"

    run git checkout "$MASTER_BRANCH"
    run git pull --ff-only

    run git checkout "$DEVELOP_BRANCH"
    run git pull --ff-only
    run git merge --no-edit "$MASTER_BRANCH"

    run git checkout "$stored_branch"
    run git merge --no-edit "$DEVELOP_BRANCH"
    run git pull --no-edit
    run git push

    echo
    echo "Git synchronization completed on ${stored_branch}."
}

main "$@"
