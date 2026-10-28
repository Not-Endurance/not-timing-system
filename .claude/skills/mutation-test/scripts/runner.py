"""Running the mutants of the mutation-test skill: one at a time, in the tree, always put back."""
from __future__ import annotations

import json
import os
import re
import shlex
import signal
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path

from mutants import load_suite, prepare, read_text, validate
from scope import Fail, repo_root


def now():
    return datetime.now(timezone.utc).isoformat(timespec='seconds')


def read_log(path):
    entries = []
    if path.exists():
        for line in path.read_text(encoding='utf-8').splitlines():
            if line.strip():
                try:
                    entries.append(json.loads(line))
                except ValueError:
                    pass
    return entries


def append_log(path, entry):
    with path.open('a', encoding='utf-8') as handle:
        handle.write(json.dumps(entry, ensure_ascii=False) + '\n')


def all_results(rd):
    """Every result of the run, oldest first: results.jsonl and the results-N.jsonl of parallel workers."""
    entries = []
    for path in [rd / 'results.jsonl', *sorted(rd.glob('results-*.jsonl'))]:
        entries += read_log(path)
    return sorted(entries, key=lambda e: e.get('time', ''))


# -------------------------------------------------------------------------------------------------- one test run


def kill_tree(process):
    if os.name == 'nt':
        subprocess.run(['taskkill', '/T', '/F', '/PID', str(process.pid)], capture_output=True)
    else:
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except OSError:
            pass


def execute(argv, cwd, timeout):
    """(exit code, output, timed out, seconds). A run that outlives its timeout is killed with everything it started."""
    environment = dict(os.environ)
    # MSBuild worker nodes that outlive the build keep folders of the tree open, and the tree cannot be removed
    environment.setdefault('MSBUILDDISABLENODEREUSE', '1')
    options = dict(
        cwd=str(cwd), env=environment, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding='utf-8', errors='replace'
    )
    if os.name == 'nt':
        options['creationflags'] = subprocess.CREATE_NEW_PROCESS_GROUP
    else:
        options['start_new_session'] = True
    started = time.time()
    process = subprocess.Popen(argv, **options)
    try:
        output, _ = process.communicate(timeout=timeout)
        return process.returncode, output, False, time.time() - started
    except subprocess.TimeoutExpired:
        kill_tree(process)
        try:
            output, _ = process.communicate(timeout=30)
        except subprocess.TimeoutExpired:
            output = ''
        return None, output, True, time.time() - started


def build_argv(template, project, tests):
    """The template split into arguments first, so a filter with spaces or '|' stays one argument."""
    return [token.replace('{project}', project).replace('{tests}', tests) for token in shlex.split(template)]


def line_with(pattern, output):
    match = re.search(pattern, output, re.M)
    if not match:
        return ''
    start = output.rfind('\n', 0, match.start()) + 1
    end = output.find('\n', match.end())
    return output[start : end if end != -1 else len(output)].strip()[:240]


def classify_result(code, output, timed_out, header):
    """(status, detail): timeout | invalid | infra | caught | survived.

    Order matters: code that does not compile, and a run in which no test ran (dotnet exits 0 for it, unittest 5), look green or
    red for the wrong reason, so they are named before caught and survived.
    """
    if timed_out:
        return 'timeout', 'no answer in time'
    if re.search(header['compile-error'], output):
        return 'invalid', 'did not compile: ' + line_with(header['compile-error'], output)
    failed = code != 0 or re.search(header['fail-marker'], output, re.M)
    if failed and re.search(header['infra'], output):
        return 'infra', line_with(header['infra'], output)
    if re.search(header['no-tests'], output):
        return 'invalid', 'no test ran: ' + line_with(header['no-tests'], output)
    return ('caught', '') if failed else ('survived', '')


def failures(output):
    """(names of the tests that went red, their first reasons), for dotnet through rtk or raw, and for unittest."""
    names, reasons = [], []
    lines = output.splitlines()
    for index, line in enumerate(lines):
        stripped = line.strip()
        if stripped == 'Error Message:':
            for back in range(index - 1, max(index - 3, -1), -1):
                candidate = lines[back].strip()
                if candidate and candidate != 'Failed Tests:':
                    candidate = re.sub(r'^(Failed|FAILED)\s+', '', candidate)
                    names.append(re.sub(r'\s*\[\d+ ?m?s\]$', '', candidate)[:160])
                    break
            if index + 1 < len(lines):
                reasons.append(lines[index + 1].strip()[:200])
            continue
        match = re.match(r'^(?:FAIL|ERROR): (.+)$', stripped)
        if match:
            names.append(match.group(1)[:160])
        elif re.match(r'^(AssertionError|[A-Za-z.]*Error|[A-Za-z.]*Exception)\b', stripped) and len(reasons) < 2:
            reasons.append(stripped[:200])
    return list(dict.fromkeys(names))[:4], list(dict.fromkeys(reasons))[:2]


def summary_of(output):
    for pattern in (r'^(?:ok|fail) dotnet test.*$', r'^(?:Passed|Failed)!.*$', r'^(?:OK|FAILED)\b.*$', r'^Ran \d+ tests?.*$'):
        match = re.search(pattern, output, re.M)
        if match:
            return match.group(0).strip()[:160]
    return ''


# -------------------------------------------------------------------------------------------------- the run loop


def recover(rd, tree, stem):
    """A run that was killed leaves a mutated file behind: put it back before anything else."""
    journal = rd / f'inflight-{stem}.json'
    if not journal.exists():
        return
    info = json.loads(journal.read_text(encoding='utf-8'))
    backup = Path(info['backup'])
    if backup.exists():
        (tree / info['file']).write_bytes(backup.read_bytes())
        print(f"restored {info['file']}, which a killed run left mutated ({info['name']})", flush=True)
    journal.unlink()


def run_baseline(header, mutants, tree, rd, stem, rebaseline):
    """Each (project, tests) pair that the mutants use must be green and non-empty unmutated, or nothing below means anything."""
    log = rd / f'baseline-{stem}.jsonl'
    done = {e['key'] for e in read_log(log) if e.get('status') == 'green'}
    pairs = {}
    for m in mutants:
        pairs.setdefault(f'{m.project}|{m.tests}', m)
    for key, m in pairs.items():
        if key in done and not rebaseline:
            continue
        print(f'baseline: {m.project} {m.tests}', flush=True)
        code, output, timed_out, seconds = execute(build_argv(header['test-command'], m.project, m.tests), tree, int(header['baseline-timeout']))
        status, detail = classify_result(code, output, timed_out, header)
        if status != 'survived':
            names, reasons = failures(output)
            raise Fail(
                f"the baseline is not green ({status}: {detail or summary_of(output)}) for {m.project} {m.tests}\n"
                + ''.join(f'  {line}\n' for line in names + reasons)
                + 'Fix the base, the filter or the machine first: a mutant proves nothing against a red or empty baseline.'
            )
        append_log(log, {'key': key, 'status': 'green', 'seconds': round(seconds, 1), 'time': now()})


def run_one(m, header, tree, rd, stem):
    path = tree / m.file
    data, text = read_text(path)
    old, new = prepare(text, m.old, m.new)
    count = text.count(old)
    if count == 0 or (count > 1 and m.occurrence == 'once'):
        detail = 'the old text is not in the file' if count == 0 else f'the old text occurs {count} times'
        return {'status': 'invalid', 'detail': detail, 'seconds': 0, 'attempts': 0, 'tests': [], 'reasons': [], 'summary': ''}
    backup = rd / f'inflight-{stem}.orig'
    backup.write_bytes(data)
    journal = rd / f'inflight-{stem}.json'
    journal.write_text(json.dumps({'file': m.file, 'name': m.name, 'backup': str(backup)}), encoding='utf-8')
    path.write_bytes(text.replace(old, new, 1).encode('utf-8', errors='surrogateescape'))
    argv = build_argv(header['test-command'], m.project, m.tests)
    attempts = 0
    try:
        while True:
            attempts += 1
            code, output, timed_out, seconds = execute(argv, tree, m.timeout)
            status, detail = classify_result(code, output, timed_out, header)
            if status == 'infra' and attempts <= int(header['retries']):
                time.sleep(10)
                continue
            break
    finally:
        path.write_bytes(data)
        if path.read_bytes() != data:
            raise Fail(f'could not put {m.file} back: restore it from {backup}')
        journal.unlink(missing_ok=True)
    names, reasons = failures(output) if status == 'caught' else ([], [])
    return {
        'status': status, 'detail': detail, 'seconds': round(seconds, 1), 'attempts': attempts,
        'tests': names, 'reasons': reasons, 'summary': summary_of(output),
    }


def cmd_run(args):
    repo = repo_root(args.repo)
    rd, default_tree, header, mutants, problems = load_suite(repo, args.run)
    tree = Path(args.root).resolve() if args.root else default_tree
    if args.test_command:
        header['test-command'] = args.test_command
    if not tree.exists():
        raise Fail(f'no tree at {tree}')
    log = Path(args.log or 'results.jsonl')
    if not log.is_absolute():
        log = rd / log
    if not re.fullmatch(r'results(-[A-Za-z0-9._]+)?\.jsonl', log.name):
        raise Fail('the results file must be called results.jsonl or results-NAME.jsonl, so that the report finds it')
    stem = log.stem
    recover(rd, tree, stem)  # before the mutants are validated: a mutated file would fail that
    problems += validate(header, mutants, tree, None)[0]
    if problems:
        raise Fail('the mutants file has problems (run check):\n  ' + '\n  '.join(problems))
    final = {e['name']: e['status'] for e in all_results(rd)}
    selected = list(mutants)
    if args.only:
        unknown = sorted(set(args.only) - {m.name for m in mutants})
        if unknown:
            raise Fail('not in the mutants file: ' + ', '.join(unknown))
        selected = [m for m in selected if m.name in args.only]
    elif not args.redo:
        selected = [m for m in selected if final.get(m.name) not in ('caught', 'survived')]
    if args.part:
        try:
            index, _, parts = args.part.partition('/')
            selected = [m for i, m in enumerate(selected) if i % int(parts) == int(index)]
        except ValueError:
            raise Fail('--part is I/N, for example 0/2') from None
    if not selected:
        print('nothing to run: every mutant has an answer (--redo runs them again)')
        return
    if not args.no_baseline:
        run_baseline(header, selected, tree, rd, stem, args.rebaseline)
    print(f'running {len(selected)} mutants in {tree}', flush=True)
    tally = {}
    for m in selected:
        result = run_one(m, header, tree, rd, stem)
        append_log(log, {'name': m.name, 'group': m.group, 'file': m.file, 'time': now(), **result})
        tally[result['status']] = tally.get(result['status'], 0) + 1
        detail = result['detail'] or (result['tests'][0] if result['tests'] else '')
        print(f"{m.name}: {result['status']} ({result['seconds']} s){' - ' + detail if detail else ''}", flush=True)
    print('done: ' + ', '.join(f'{k} {v}' for k, v in sorted(tally.items())), flush=True)
