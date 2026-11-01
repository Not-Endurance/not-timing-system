"""Git side of the mutation-test skill: what a change touched, and the throwaway tree the mutants are applied in.

Git is called plainly, not through rtk: its output is parsed here and rtk would compact it.
"""
from __future__ import annotations

import dataclasses
import json
import os
import re
import shutil
import stat
import subprocess
from pathlib import Path

EMPTY_TREE = '4b825dc642cb6eb9a060e54bf8d69288fbee4904'
RUNS = Path('.tmp') / 'mutation-test'

CODE_EXTENSIONS = {
    '.cs', '.razor', '.fs', '.vb', '.js', '.mjs', '.ts', '.tsx', '.jsx', '.py', '.go', '.rs', '.java', '.kt',
    '.c', '.cc', '.cpp', '.h', '.hpp', '.sh', '.ps1', '.sql', '.css',
}
CONFIG_EXTENSIONS = {
    '.csproj', '.props', '.targets', '.sln', '.slnx', '.json', '.yaml', '.yml', '.xml', '.config', '.toml', '.ini',
    '.editorconfig', '.gitignore', '.gitattributes', '.resx', '.dockerignore',
}
DOC_EXTENSIONS = {'.md', '.txt', '.rst', '.adoc'}


class Fail(Exception):
    """A problem to tell the user in a line, not a traceback."""


def git(args, cwd, check=True):
    result = subprocess.run(
        ['git', '-c', 'core.quotepath=off', *args],
        cwd=str(cwd),
        capture_output=True,
        text=True,
        encoding='utf-8',
        errors='replace',
    )
    if check and result.returncode != 0:
        raise Fail(f'git {" ".join(args)}: {(result.stderr or result.stdout).strip()}')
    return result.stdout


def repo_root(start):
    return Path(git(['rev-parse', '--show-toplevel'], start or Path.cwd()).strip())


def run_dir(repo, run):
    if not re.fullmatch(r'[A-Za-z0-9._-]+', run or ''):
        raise Fail(f'--run must be a short name of letters, digits, . _ - (got {run!r})')
    return repo / RUNS / run


def ensure_excluded(repo):
    """Keep .tmp/mutation-test out of `git status` in a repository that does not ignore .tmp."""
    try:
        path = Path(git(['rev-parse', '--path-format=absolute', '--git-path', 'info/exclude'], repo).strip())
    except Fail:
        return
    entry = '.tmp/mutation-test/'
    path.parent.mkdir(parents=True, exist_ok=True)
    text = path.read_text(encoding='utf-8') if path.exists() else ''
    if entry not in text.splitlines():
        path.write_text(text + ('' if text.endswith('\n') or not text else '\n') + entry + '\n', encoding='utf-8')


def shutdown_build_servers():
    """A compiler or build server started in the tree keeps it locked on Windows."""
    try:
        subprocess.run(['dotnet', 'build-server', 'shutdown'], capture_output=True, timeout=60)
    except (OSError, subprocess.TimeoutExpired):
        pass


def rmtree(path):
    def writable(function, target, _info):
        os.chmod(target, stat.S_IWRITE)
        function(target)

    shutil.rmtree(path, onerror=writable)


# ---------------------------------------------------------------------------------------------------------- scope


@dataclasses.dataclass
class Span:
    label: str
    base: str
    head: str | None  # None: the working tree


def resolve_span(repo, spec, last, working_tree):
    """The commits a request means: nothing = the latest commit, a commit = that one, A..B = a range, --last N."""
    if working_tree:
        return Span('uncommitted changes', 'HEAD', None)
    if last:
        base = git(['rev-parse', '--verify', f'HEAD~{last}^{{commit}}'], repo).strip()
        head = git(['rev-parse', '--verify', 'HEAD^{commit}'], repo).strip()
        return Span(f'last {last} commits', base, head)
    spec = spec or 'HEAD'
    if '..' in spec:
        three = '...' in spec
        left, _, right = spec.partition('...' if three else '..')
        base = git(['rev-parse', '--verify', f'{left or "HEAD"}^{{commit}}'], repo).strip()
        head = git(['rev-parse', '--verify', f'{right or "HEAD"}^{{commit}}'], repo).strip()
        if three:
            base = git(['merge-base', base, head], repo).strip()
        return Span(spec, base, head)
    head = git(['rev-parse', '--verify', f'{spec}^{{commit}}'], repo).strip()
    parents = git(['rev-list', '--parents', '-n', '1', head], repo).split()
    return Span(f'commit {head[:9]}', parents[1] if len(parents) > 1 else EMPTY_TREE, head)


def classify(path):
    """code | test | config | docs | other, from the path alone."""
    original = path.replace('\\', '/').split('/')
    parts = [part.lower() for part in original]
    name = parts[-1]
    extension = os.path.splitext(name)[1]
    if parts[0] in ('.github', '.vscode') or extension in CONFIG_EXTENSIONS or name.startswith('directory.build'):
        return 'config'
    if parts[0] in ('docs', '.claude', '.codex') or extension in DOC_EXTENSIONS:
        return 'docs'
    in_tests = any(re.search(r'(^|[._-])(tests?|specs?)($|[._-])', part) for part in parts[:-1])
    # the case of the original name tells FooTests.cs from latest.cs
    stem = os.path.splitext(original[-1])[0]
    if in_tests or re.search(r'(^tests?_|^tests?\.|(Tests?|Specs?)$|[._-](tests?|specs?)$)', stem):
        return 'test'
    return 'code' if extension in CODE_EXTENSIONS else 'other'


def parse_diff(text):
    files, current = [], None
    for line in text.split('\n'):
        if line.startswith('diff --git '):
            rest = line[len('diff --git ') :]
            half = (len(rest) - 1) // 2
            if len(rest) % 2 == 1 and rest[:half].startswith('a/') and rest[half + 1 :] == 'b/' + rest[:half][2:]:
                path = rest[half + 3 :]
            else:
                match = re.match(r'a/(.+?) b/(.+)$', rest)
                path = match.group(2) if match else rest
            current = {
                'path': path, 'old_path': None, 'status': 'M', 'similarity': None,
                'ranges': [], 'added': 0, 'removed': 0, 'binary': False,
            }
            files.append(current)
        elif current is None:
            continue
        elif line.startswith('new file mode'):
            current['status'] = 'A'
        elif line.startswith('deleted file mode'):
            current['status'] = 'D'
        elif line.startswith('similarity index '):
            current['similarity'] = int(line.split()[-1].rstrip('%'))
        elif line.startswith('rename from '):
            current['old_path'], current['status'] = line[len('rename from ') :], 'R'
        elif line.startswith('rename to '):
            current['path'] = line[len('rename to ') :]
        elif line.startswith('Binary files'):
            current['binary'] = True
        elif line.startswith('--- ') and current['status'] == 'D' and line[4:] != '/dev/null':
            current['path'] = line[6:].split('\t')[0]
        elif line.startswith('+++ ') and line[4:] != '/dev/null':
            current['path'] = line[6:].split('\t')[0]
        elif line.startswith('@@'):
            match = re.match(r'@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@', line)
            if match:
                current['removed'] += int(match.group(2)) if match.group(2) is not None else 1
                start = int(match.group(3))
                count = int(match.group(4)) if match.group(4) is not None else 1
                current['added'] += count
                if count:
                    current['ranges'].append([start, start + count - 1])
    for entry in files:
        entry['kind'] = classify(entry['path'])
    return files


def untracked_files(repo):
    entries = []
    for name in git(['ls-files', '-o', '--exclude-standard', '-z'], repo).split('\0'):
        if not name or name.startswith('.tmp/'):
            continue
        try:
            data = (repo / name).read_bytes()
        except OSError:
            continue
        binary = b'\0' in data[:4096]
        lines = data.count(b'\n') + (0 if data.endswith(b'\n') or not data else 1)
        entries.append({
            'path': name, 'old_path': None, 'status': 'A', 'similarity': None,
            'ranges': [[1, lines]] if lines and not binary else [], 'added': lines, 'removed': 0,
            'binary': binary, 'kind': classify(name),
        })
    return entries


def scope_of(repo, span):
    arguments = ['diff', '-M', '-U0', '--no-color', '--no-ext-diff', span.base]
    if span.head:
        arguments.append(span.head)
    files = parse_diff(git(arguments, repo))
    if span.head is None:
        files += untracked_files(repo)
    head = span.head or git(['rev-parse', 'HEAD'], repo).strip()
    return {'label': span.label, 'base': span.base, 'head': head, 'working_tree': span.head is None, 'files': files}


def describe_ranges(ranges):
    return ', '.join(str(a) if a == b else f'{a}-{b}' for a, b in ranges)


def print_scope(scope, only_code):
    order = {'code': 0, 'test': 1, 'config': 2, 'docs': 3, 'other': 4}
    files = sorted(scope['files'], key=lambda f: (order.get(f['kind'], 9), f['path']))
    tail = ', plus the working tree' if scope['working_tree'] else ''
    print(f"Scope: {scope['label']}  ({scope['base'][:9]}..{scope['head'][:9]}{tail})")
    counts, skipped = {}, {}
    for entry in files:
        counts[entry['kind']] = counts.get(entry['kind'], 0) + 1
        if only_code and entry['kind'] != 'code':
            continue
        if only_code and not entry['ranges']:
            why = 'deleted' if entry['status'] == 'D' else 'moved or changed without a line'
            skipped[why] = skipped.get(why, 0) + 1
            continue
        notes = []
        if entry['status'] == 'R':
            unchanged = not entry['ranges'] and not entry['removed']
            notes.append(f"moved from {entry['old_path']}" + (' without a change' if unchanged else ''))
        if entry['binary']:
            notes.append('binary')
        where = f"  lines {describe_ranges(entry['ranges'])}" if entry['ranges'] else ''
        extra = f"  [{'; '.join(notes)}]" if notes else ''
        print(f"{entry['kind']:<6} {entry['status']}  +{entry['added']}/-{entry['removed']:<5} {entry['path']}{where}{extra}")
    print('Files: ' + ', '.join(f'{n} {k}' for k, n in sorted(counts.items(), key=lambda kv: order.get(kv[0], 9))))
    if skipped:
        print('Not listed, nothing to mutate: ' + ', '.join(f'{n} code files {why}' for why, n in sorted(skipped.items())))


def cmd_scope(args):
    repo = repo_root(args.repo)
    scope = scope_of(repo, resolve_span(repo, args.spec, args.last, args.working_tree))
    if args.json:
        print(json.dumps(scope, indent=1))
    else:
        print_scope(scope, args.code)


# ----------------------------------------------------------------------------------------------------------- tree


def sync_files(repo, tree):
    """Copy what differs from HEAD in the working tree into the tree: new tests, and the code of a working-tree scope."""
    entries = git(['status', '--porcelain=v1', '-z', '--untracked-files=all'], repo).split('\0')
    copied = removed = 0
    index = 0
    while index < len(entries):
        entry = entries[index]
        index += 1
        if len(entry) < 4:
            continue
        status, path = entry[:2], entry[3:]
        old = None
        if 'R' in status or 'C' in status:
            old = entries[index] if index < len(entries) else None
            index += 1
        if path.startswith('.tmp/'):
            continue
        if old and (tree / old).exists():
            (tree / old).unlink()
            removed += 1
        source, target = repo / path, tree / path
        if not source.exists():
            if target.exists():
                target.unlink()
                removed += 1
            continue
        if source.is_dir():
            continue
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        copied += 1
    return copied, removed


def cmd_tree(args):
    repo = repo_root(args.repo)
    rd = run_dir(repo, args.run)
    tree = rd / 'tree'
    if args.action == 'create':
        if rd.exists():
            raise Fail(f'{rd} exists: remove it (tree remove --run {args.run}) or pick another --run')
        span = resolve_span(repo, args.spec, args.last, args.working_tree)
        scope = scope_of(repo, span)
        ensure_excluded(repo)
        rd.mkdir(parents=True)
        (rd / 'scope.json').write_text(json.dumps(scope, indent=1), encoding='utf-8')
        git(['worktree', 'add', '--detach', str(tree), scope['head']], repo)
        if span.head is None:
            copied, removed = sync_files(repo, tree)
            print(f'working tree: {copied} files copied into the tree, {removed} removed')
        print(f"run {args.run}: tree {tree} at {scope['head'][:9]}, {len(scope['files'])} files in the scope")
        print(f"next: write {rd / 'mutants.txt'}")
    elif args.action == 'sync':
        if not tree.exists():
            raise Fail(f'no tree at {tree}')
        copied, removed = sync_files(repo, tree)
        for baseline in rd.glob('baseline*.jsonl'):
            baseline.unlink()
        print(f'synced {copied} files into the tree, {removed} removed; the next run measures the baseline again')
    elif args.action == 'remove':
        shutdown_build_servers()
        if tree.exists():
            git(['worktree', 'remove', '--force', str(tree)], repo, check=False)
        if rd.exists():
            try:
                rmtree(rd)
            except OSError as error:
                raise Fail(
                    f'{error}. Something still holds the tree open (an IDE that indexed it, a process started from it): '
                    f'close it and run tree remove --run {args.run} again'
                ) from None
        git(['worktree', 'prune'], repo, check=False)
        try:
            rd.parent.rmdir()  # the folder of all runs, when this was the last
        except OSError:
            pass
        print(f'removed {rd}')
