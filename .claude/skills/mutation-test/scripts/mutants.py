"""The mutants file of the mutation-test skill: reading it, validating it against the tree, and what of the change it covers."""
from __future__ import annotations

import dataclasses
import json
import re

from scope import Fail, classify, repo_root, run_dir

DEFAULTS = {
    'test-command': '',
    'project': '',
    'tests': '',
    'timeout': '900',
    'baseline-timeout': '1800',
    'retries': '2',
    'compile-error': r'\berror (?:CS|MSB|NETSDK|FS|BC|TS)\d+\b|Build FAILED|SyntaxError|IndentationError',
    'infra': (
        r'ContainerNotRunningException|exited with code 48|Cannot connect to the Docker|Docker (?:daemon|API)'
        r'|ResourceReaper|address already in use|MSB4166'
    ),
    'no-tests': (
        r'No test matches the given testcase filter|No test is available|counts unavailable'
        r'|ok dotnet test: 0 tests|Total tests: 0|\bRan 0 tests'
    ),
    'fail-marker': r'\bfail dotnet test\b|\bFailed!|^\s*Failed\s+\S+|FAILED \(',
}
KEYS_OF_A_MUTANT = {'file', 'tests', 'project', 'timeout', 'group', 'note', 'occurrence', 'old', 'new'}


@dataclasses.dataclass
class Mutant:
    name: str
    file: str
    old: str
    new: str
    tests: str
    project: str
    timeout: int
    group: str
    occurrence: str
    note: str
    line: int


def parse_mutants(text):
    """(header, mutants, problems). Inline `old:`/`new:` hold a piece of one line; a block holds whole lines."""
    header, mutants, problems = dict(DEFAULTS), [], []
    state = {'current': None, 'fields': {}, 'block': None}

    def finish():
        current, fields = state['current'], state['fields']
        state['current'], state['fields'] = None, {}
        if current is None:
            return
        name, start = current
        missing = [k for k in ('file', 'old', 'new') if k not in fields]
        for key in missing:
            problems.append(f'line {start}: {name} has no {key}')
        if missing:
            return
        try:
            timeout = int(fields.get('timeout', header['timeout']) or 900)
        except ValueError:
            problems.append(f'line {start}: {name} has a timeout that is not a number')
            return
        mutants.append(Mutant(
            name=name, file=fields['file'].strip(), old=fields['old'], new=fields['new'],
            tests=fields.get('tests', header['tests']).strip(), project=fields.get('project', header['project']).strip(),
            timeout=timeout, group=fields.get('group', name.split('-')[0]).strip(),
            occurrence=fields.get('occurrence', 'once').strip(), note=fields.get('note', ''), line=start,
        ))

    for number, line in enumerate(text.replace('\r\n', '\n').split('\n'), start=1):
        fields = state['fields']
        if state['block'] is not None:
            if state['block'] == 'old' and line == '=======':
                state['block'] = 'new'
                fields['new'] = ''
            elif state['block'] == 'new' and line.startswith('>>>>>>> new'):
                state['block'] = None
            else:
                fields[state['block']] = fields.get(state['block'], '') + line + '\n'
            continue
        if line.startswith('=== '):
            finish()
            name = line[4:].strip()
            if not re.fullmatch(r'[a-z0-9._-]+', name):
                problems.append(f'line {number}: the name {name!r} is not made of [a-z0-9._-]')
            state['current'] = (name, number)
            continue
        if line.startswith('<<<<<<< old'):
            if state['current'] is None:
                problems.append(f'line {number}: a block outside a mutant')
            state['block'] = 'old'
            fields['old'] = ''
            continue
        if not line.strip() or line.startswith('#'):
            continue
        match = re.match(r'([a-z-]+): ?(.*)$', line)
        if not match:
            problems.append(f'line {number}: not understood: {line[:60]!r}')
            continue
        key, value = match.group(1), match.group(2)
        if state['current'] is None:
            if key in DEFAULTS:
                header[key] = value
            else:
                problems.append(f'line {number}: unknown header key {key!r}')
        elif key in KEYS_OF_A_MUTANT:
            fields[key] = value
        else:
            problems.append(f"line {number}: unknown key {key!r} in {state['current'][0]}")
    if state['block'] is not None:
        problems.append('a <<<<<<< old block was not closed with >>>>>>> new')
    finish()
    names = [m.name for m in mutants]
    for name in sorted({n for n in names if names.count(n) > 1}):
        problems.append(f'the name {name} is used twice')
    return header, mutants, problems


def read_text(path):
    data = path.read_bytes()
    return data, data.decode('utf-8', errors='surrogateescape')


def prepare(text, old, new):
    """The old and new text in the line endings of the file."""
    if '\r\n' in text:
        return old.replace('\n', '\r\n'), new.replace('\n', '\r\n')
    return old, new


def locate(text, old):
    """(how many times, first line, last line) of the first occurrence."""
    count = text.count(old)
    if not count:
        return 0, None, None
    first = text.count('\n', 0, text.find(old)) + 1
    return count, first, first + old.rstrip('\r\n').count('\n')


def coverage(scope, mutants, tree):
    """(changed code hunks that no mutant touches, mutants that lie outside the changed lines)."""
    spans, outside = {}, []
    changed = {f['path']: f for f in scope['files']} if scope else {}
    for mutant in mutants:
        path = tree / mutant.file
        if not path.is_file():
            continue
        _, text = read_text(path)
        count, first, last = locate(text, prepare(text, mutant.old, mutant.new)[0])
        if not count:
            continue
        spans.setdefault(mutant.file, []).append((first, last))
        entry = changed.get(mutant.file)
        if scope and (entry is None or not any(a <= last and first <= b for a, b in entry['ranges'])):
            outside.append(mutant.name)
    untouched = []
    for entry in changed.values():
        if entry['kind'] != 'code':
            continue
        for a, b in entry['ranges']:
            if not any(first <= b and a <= last for first, last in spans.get(entry['path'], [])):
                untouched.append((entry['path'], a, b))
    return untouched, outside


def validate(header, mutants, tree, scope):
    """(problems that stop a run, warnings, untouched changed code)."""
    problems, warnings = [], []
    if not header['test-command']:
        problems.append('the header has no test-command')
    for m in mutants:
        path = tree / m.file
        if not path.is_file():
            problems.append(f'{m.name}: {m.file} is not in the tree')
            continue
        if not m.old:
            problems.append(f'{m.name}: old is empty')
        elif m.old == m.new:
            problems.append(f'{m.name}: old and new are the same')
        else:
            _, text = read_text(path)
            count = locate(text, prepare(text, m.old, m.new)[0])[0]
            if count == 0:
                problems.append(f'{m.name}: the old text is not in {m.file}')
            elif count > 1 and m.occurrence == 'once':
                problems.append(f'{m.name}: the old text occurs {count} times in {m.file}: lengthen it, or say occurrence: first')
        if '{tests}' in header['test-command'] and not m.tests:
            problems.append(f'{m.name}: no tests')
        if '{project}' in header['test-command'] and not m.project:
            problems.append(f'{m.name}: no project')
        kind = classify(m.file)
        if kind != 'code':
            warnings.append(f'{m.name}: {m.file} is {kind}, not code: fine only when a test reads it as it runs')
    untouched, outside = coverage(scope, mutants, tree)
    warnings += [f'{name}: lies outside the changed lines of the scope' for name in outside]
    return problems, warnings, untouched


def load_suite(repo, run):
    rd = run_dir(repo, run)
    path = rd / 'mutants.txt'
    if not path.exists():
        raise Fail(f'no mutants file at {path}')
    header, mutants, problems = parse_mutants(path.read_text(encoding='utf-8'))
    return rd, rd / 'tree', header, mutants, problems


def load_scope(rd):
    path = rd / 'scope.json'
    return json.loads(path.read_text(encoding='utf-8')) if path.exists() else None


def cmd_check(args):
    repo = repo_root(args.repo)
    rd, tree, header, mutants, problems = load_suite(repo, args.run)
    warnings, untouched = [], []
    if tree.exists():
        more, warnings, untouched = validate(header, mutants, tree, load_scope(rd))
        problems += more
    else:
        problems.append(f'no tree at {tree}')
    groups = {}
    for m in mutants:
        groups[m.group] = groups.get(m.group, 0) + 1
    print(f'{len(mutants)} mutants in {len(groups)} groups: ' + ', '.join(f'{g} {n}' for g, n in sorted(groups.items())))
    for warning in warnings:
        print('warning:', warning)
    if untouched:
        print('changed code that no mutant touches (mutate it, or have a reason to skip it):')
        for path, a, b in untouched[:40]:
            print(f'  {path}:{a if a == b else f"{a}-{b}"}')
        if len(untouched) > 40:
            print(f'  and {len(untouched) - 40} more')
    for problem in problems:
        print('problem:', problem)
    if problems:
        raise Fail(f'{len(problems)} problems')
    print('ok')
