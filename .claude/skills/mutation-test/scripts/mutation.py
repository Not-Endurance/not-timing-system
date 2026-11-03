#!/usr/bin/env python3
"""Helper of the mutation-test skill: scope a change, make an isolated tree, check and run mutants, report.

    python -X utf8 -I mutation.py scope  [SPEC] [--last N] [--working-tree] [--code] [--json]
    python -X utf8 -I mutation.py tree   create|sync|remove [SPEC] --run NAME [--last N] [--working-tree]
    python -X utf8 -I mutation.py check  --run NAME
    python -X utf8 -I mutation.py run    --run NAME [--only NAME ...] [--part I/N] [--root TREE] [--log FILE] [--redo]
    python -X utf8 -I mutation.py report --run NAME [--dispositions FILE] [--out FILE]

SPEC comes before the options. It is a commit (that commit alone), a range A..B, or nothing (the latest commit). --last N is HEAD~N..HEAD and
--working-tree the uncommitted changes. --repo PATH chooses another repository than the current directory.
A run is the folder .tmp/mutation-test/NAME/ of the repository: tree/ (a git worktree at the head commit, the only place a
mutant is ever applied), scope.json, mutants.txt (written by you), results.jsonl, dispositions.txt (written by you).

THE MUTANTS FILE (mutants.txt)

    # a comment, outside a block
    test-command: rtk dotnet test {project} -v:m --filter {tests}    <- the header, before the first mutant
    project: tests/NTS.Tests.Unit/NTS.Tests.Unit.csproj              <- defaults of every mutant
    timeout: 900

    === ui-wrong-project                  <- one mutant: a unique name of [a-z0-9._-]
    file: src/Api/Rules.cs                <- relative to the tree, forward slashes
    tests: FullyQualifiedName~RulesTests  <- becomes {tests}: the tests that should notice it
    old: var ui = graph.Get(UI);          <- inline form: the rest of the line, exactly; a piece of one line
    new: var ui = graph.Get(API);

    === retired-names-gone
    file: src/Api/Rules.cs
    tests: FullyQualifiedName~RulesTests
    <<<<<<< old                           <- block form: whole lines, each with its newline, no escaping
            "NTS.Application",
            "NTS.Storage",
    =======
            "NTS.Application",
    >>>>>>> new                           <- an empty new part deletes the lines

Keys of a mutant besides file, tests, old and new: project and timeout (override the header), group (default: the name
before its first '-'), note, and occurrence: first (the old text may occur more than once; the first is changed; default:
it must occur once). Keys of the header besides test-command and the defaults above: baseline-timeout (1800), retries (2,
for infra failures) and the regular expressions compile-error, infra, no-tests and fail-marker (the defaults suit dotnet,
rtk and unittest). {project} and {tests} in test-command each become one argument, so a filter may hold spaces and '|'.

A result is one of: caught (a test went red), survived (all green), invalid (it did not compile, its old text was not
found, or no test ran: it proves nothing), infra (a container or the machine failed, tried again) and timeout.
dispositions.txt explains survivors, one per line: NAME: equivalent|no-seam|dead-code the reason.
"""
from __future__ import annotations

import argparse
import os
import re
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from mutants import cmd_check, coverage, load_scope, load_suite  # noqa: E402
from runner import all_results, cmd_run  # noqa: E402
from scope import Fail, cmd_scope, cmd_tree, repo_root, run_dir  # noqa: E402

DISPOSITION = re.compile(r'^([a-z0-9._-]+):\s*(equivalent|no-seam|dead-code)\b[\s:.-]*(.*)$')


def cmd_report(args):
    repo = repo_root(args.repo)
    rd = run_dir(repo, args.run)
    mutants, tree = [], rd / 'tree'
    if (rd / 'mutants.txt').exists():
        _, tree, _, mutants, _ = load_suite(repo, args.run)
    history = {}
    for entry in all_results(rd):
        history.setdefault(entry['name'], []).append(entry)
    dispositions = {}
    path = Path(args.dispositions) if args.dispositions else rd / 'dispositions.txt'
    if path.exists():
        for line in path.read_text(encoding='utf-8').splitlines():
            match = DISPOSITION.match(line.strip())
            if match:
                dispositions[match.group(1)] = (match.group(2), match.group(3).strip())
    by_name = {m.name: m for m in mutants}
    names = [m.name for m in mutants] or sorted(history)
    last = {n: (history[n][-1] if n in history else None) for n in names}
    status = {n: (last[n]['status'] if last[n] else 'missing') for n in names}
    caught = [n for n in names if status[n] == 'caught']
    closed = [n for n in caught if any(e['status'] == 'survived' for e in history[n])]
    survivors = [n for n in names if status[n] == 'survived']
    explained = {k: [n for n in survivors if dispositions.get(n, ('', ''))[0] == k] for k in ('equivalent', 'no-seam', 'dead-code')}
    unexplained = [n for n in survivors if n not in dispositions]
    unanswered = [n for n in names if status[n] in ('invalid', 'infra', 'timeout', 'missing')]
    scope = load_scope(rd)
    answered = len(caught) + len(survivors)
    out = [f"## Mutation test: {scope['label'] if scope else args.run}"]
    if scope:
        tail = ' plus the working tree' if scope['working_tree'] else ''
        code_files = sum(1 for f in scope['files'] if f['kind'] == 'code')
        out.append(f"Scope `{scope['base'][:9]}..{scope['head'][:9]}`{tail}, {code_files} code files changed.")
    percent = f' ({100 * len(caught) // answered}%)' if answered else ''
    out += [
        '', '| | |', '|---|---|',
        f'| Mutants written | {len(names)} |',
        f'| Caught | {len(caught)} of {answered} answered{percent} |',
        f'| Survived first, caught after a new test | {len(closed)} |',
        f"| Explained without a test | equivalent {len(explained['equivalent'])}, no seam {len(explained['no-seam'])}, dead code {len(explained['dead-code'])} |",
        f'| Still surviving, unexplained | {len(unexplained)} |',
        f'| No answer (did not compile, no test ran, infra, timeout) | {len(unanswered)} |',
    ]
    groups = {}
    for n in names:
        row = groups.setdefault(by_name[n].group if n in by_name else n.split('-')[0], [0, 0])
        row[0] += 1
        row[1] += status[n] == 'caught'
    if len(groups) > 1:
        out += ['', '### By area', '', '| area | mutants | caught |', '|---|---|---|']
        out += [f'| {g} | {a} | {b} |' for g, (a, b) in sorted(groups.items())]
    if survivors:
        out += ['', '### Survivors', '', '| mutant | where | disposition |', '|---|---|---|']
        for n in survivors:
            kind, why = dispositions.get(n, ('UNEXPLAINED', ''))
            where = f'`{by_name[n].file}:{by_name[n].line}`' if n in by_name else ''
            out.append(f"| {n} | {where} | {kind}{': ' + why if why else ''} |")
    if unanswered:
        out += ['', '### Without an answer', '']
        out += [f"- {n}: {status[n]}" + (f" ({last[n]['detail']})" if last[n] and last[n].get('detail') else '') for n in unanswered]
    if tree.exists() and mutants:
        untouched, outside = coverage(scope, mutants, tree)
        if untouched:
            out += ['', '### Changed code that no mutant touches', '']
            out += [f"- `{p}:{a if a == b else f'{a}-{b}'}`" for p, a, b in untouched[:40]]
            if len(untouched) > 40:
                out.append(f'- and {len(untouched) - 40} more')
        if outside:
            out += ['', 'Mutants outside the changed lines: ' + ', '.join(outside)]
    text = '\n'.join(out) + '\n'
    if args.out:
        Path(args.out).write_text(text, encoding='utf-8')
    print(text)


def main(argv):
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--repo', help='the repository (default: the current directory)')
    sub = parser.add_subparsers(dest='command', required=True)

    def spec(p):
        p.add_argument('spec', nargs='?', help='a commit, a range A..B, or nothing: the latest commit')
        p.add_argument('--last', type=int, help='the last N commits')
        p.add_argument('--working-tree', action='store_true', help='the uncommitted changes')

    p = sub.add_parser('scope', help='the changed files by kind, with the changed lines')
    spec(p)
    p.add_argument('--code', action='store_true', help='only the code files')
    p.add_argument('--json', action='store_true')
    p = sub.add_parser('tree', help='create, sync or remove the throwaway worktree of a run')
    p.add_argument('action', choices=['create', 'sync', 'remove'])
    p.add_argument('--run', default='r1')
    spec(p)
    p = sub.add_parser('check', help='validate mutants.txt against the tree and the scope')
    p.add_argument('--run', default='r1')
    p = sub.add_parser('run', help='the baseline, then every mutant that has no answer')
    p.add_argument('--run', default='r1')
    p.add_argument('--root', help="another tree than the run's (each parallel worker has its own)")
    p.add_argument('--log', help='results.jsonl, or results-NAME.jsonl for a parallel worker')
    p.add_argument('--only', nargs='+', help='run these mutants again')
    p.add_argument('--part', help='I/N: every Nth mutant, starting at I')
    p.add_argument('--redo', action='store_true', help='run the mutants that have an answer again')
    p.add_argument('--no-baseline', action='store_true')
    p.add_argument('--rebaseline', action='store_true')
    p.add_argument('--test-command', help='instead of the test-command of the header')
    p = sub.add_parser('report', help='a Markdown report of the results')
    p.add_argument('--run', default='r1')
    p.add_argument('--dispositions', help='file of "NAME: equivalent|no-seam|dead-code reason" lines')
    p.add_argument('--out')
    args = parser.parse_args(argv)
    commands = {'scope': cmd_scope, 'tree': cmd_tree, 'check': cmd_check, 'run': cmd_run, 'report': cmd_report}
    try:
        commands[args.command](args)
    except Fail as error:
        print(f'mutation.py: {error}', file=sys.stderr)
        return 2
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
