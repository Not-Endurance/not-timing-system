#!/usr/bin/env python3
"""Self-test of mutation.py on a toy repository (Python and unittest), through its command line.

    python -X utf8 -I selftest.py [--keep]

The toy lives in .tmp/mutation-test-selftest/ of the repository that holds this skill and is removed when the test ends.
It shows that a mutant is caught, survives, does not compile, hangs, is found invalid or equivalent, that a killed run is put
back, that a new test is synced into the tree and closes a survivor, and that the report counts all of it.
"""
from __future__ import annotations

import json
import os
import shutil
import stat
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
SCRIPT = HERE / 'mutation.py'

CALC_1 = 'def add(a, b):\n    return a + b\n'
CALC_2 = CALC_1 + '''

def clamp(x, lo, hi):
    if x < lo:
        return lo
    if x > hi:
        return hi
    return x
'''
TEST_1 = '''import unittest
from calc import add


class AddTests(unittest.TestCase):
    def test_add(self):
        self.assertEqual(add(1, 2), 3)
'''
TEST_2 = TEST_1.replace('from calc import add', 'from calc import add, clamp') + '''

class ClampTests(unittest.TestCase):
    def test_below(self):
        self.assertEqual(clamp(-5, 0, 10), 0)

    def test_inside(self):
        self.assertEqual(clamp(5, 0, 10), 5)
'''
TEST_MORE = '''import unittest
from calc import clamp


class UpperTests(unittest.TestCase):
    def test_above(self):
        self.assertEqual(clamp(50, 0, 10), 10)
'''
MUTANTS = '''# the toy
test-command: python -X utf8 -m unittest discover -s . -p {tests}
tests: test_*.py
timeout: 60

=== clamp-lower-wrong
file: calc.py
old: return lo
new: return hi

=== clamp-upper-wrong
file: calc.py
old: return hi
new: return lo

=== clamp-no-syntax
file: calc.py
old: def clamp(x, lo, hi):
new: def clamp(x, lo, hi)

=== clamp-hangs
file: calc.py
timeout: 3
old: return x
new: while True: pass

=== clamp-boundary
file: calc.py
old: if x < lo:
new: if x <= lo:

=== clamp-upper-gone
file: calc.py
<<<<<<< old
    if x > hi:
        return hi
=======
>>>>>>> new
'''
NOTHING_RAN = '''test-command: python -X utf8 -m unittest discover -s . -p {tests}
timeout: 60

=== nothing-ran
file: calc.py
tests: nothing_matches_this_*.py
old: return lo
new: return hi
'''


def writable(function, target, _info):
    os.chmod(target, stat.S_IWRITE)
    function(target)


def sh(*args, cwd, check=True):
    return subprocess.run(args, cwd=str(cwd), capture_output=True, text=True, encoding='utf-8', errors='replace', check=check).stdout


def main():
    keep = '--keep' in sys.argv
    outer = subprocess.run(['git', 'rev-parse', '--show-toplevel'], cwd=str(HERE), capture_output=True, text=True)
    base = Path(outer.stdout.strip()) if outer.returncode == 0 else Path.cwd()
    folder = base / '.tmp' / 'mutation-test-selftest'
    if folder.exists():
        shutil.rmtree(folder, onerror=writable)
    toy = folder / 'toy'
    toy.mkdir(parents=True)
    failures = []

    def check(condition, message):
        print(('ok    ' if condition else 'FAIL  ') + message)
        if not condition:
            failures.append(message)

    def mt(*args, expect=0):
        result = subprocess.run(
            [sys.executable, '-X', 'utf8', '-I', str(SCRIPT), '--repo', str(toy), *args],
            capture_output=True, text=True, encoding='utf-8', errors='replace',
        )
        if result.returncode != expect:
            print(result.stdout, result.stderr)
        check(result.returncode == expect, f"mutation.py {' '.join(args)} exits {expect}")
        return result.stdout + result.stderr

    try:
        sh('git', 'init', '-q', cwd=toy)
        for key, value in (('user.name', 'selftest'), ('user.email', 'selftest@example.org'), ('core.autocrlf', 'false')):
            sh('git', 'config', key, value, cwd=toy)
        (toy / '.gitignore').write_text('.tmp/\n__pycache__/\n', encoding='utf-8')
        (toy / 'calc.py').write_text(CALC_1, encoding='utf-8')
        (toy / 'test_calc.py').write_text(TEST_1, encoding='utf-8')
        sh('git', 'add', '-A', cwd=toy)
        sh('git', 'commit', '-q', '-m', 'one', cwd=toy)
        (toy / 'calc.py').write_text(CALC_2, encoding='utf-8')
        (toy / 'test_calc.py').write_text(TEST_2, encoding='utf-8')
        sh('git', 'add', '-A', cwd=toy)
        sh('git', 'commit', '-q', '-m', 'two: clamp', cwd=toy)

        out = mt('scope', '--code')
        check('calc.py' in out and 'test_calc.py' not in out, 'scope --code lists the changed code file and not the test')
        data = json.loads(mt('scope', '--json'))
        kinds = {f['path']: f['kind'] for f in data['files']}
        check(kinds == {'calc.py': 'code', 'test_calc.py': 'test'}, 'scope --json classifies code and test')
        calc = next(f for f in data['files'] if f['path'] == 'calc.py')
        check(any(a <= 5 and b >= 10 for a, b in calc['ranges']), 'scope has the changed lines of clamp')

        mt('tree', 'create', '--run', 't1')
        run = toy / '.tmp' / 'mutation-test' / 't1'
        check((run / 'tree' / 'calc.py').exists() and (run / 'scope.json').exists(), 'tree create makes the worktree and scope.json')
        mt('tree', 'create', '--run', 't1', expect=2)

        (run / 'mutants.txt').write_text(MUTANTS, encoding='utf-8')
        out = mt('check', '--run', 't1')
        check('6 mutants' in out and out.strip().endswith('ok'), 'check accepts the toy mutants')
        check('no mutant touches' not in out, 'every changed hunk of code has a mutant')
        (run / 'bad.txt').write_text('=== x\nfile: calc.py\nold: nothing like this\nnew: y\n', encoding='utf-8')
        shutil.copy(run / 'mutants.txt', run / 'good.txt')
        shutil.copy(run / 'bad.txt', run / 'mutants.txt')
        out = mt('check', '--run', 't1', expect=2)
        check('the old text is not in calc.py' in out and 'has no tests' not in out, 'check names an old text that is not in the file')
        shutil.copy(run / 'good.txt', run / 'mutants.txt')

        out = mt('run', '--run', 't1')
        results = {}
        for line in (run / 'results.jsonl').read_text(encoding='utf-8').splitlines():
            entry = json.loads(line)
            results[entry['name']] = entry['status']
        expected = {
            'clamp-lower-wrong': 'caught', 'clamp-upper-wrong': 'survived', 'clamp-no-syntax': 'invalid', 'clamp-hangs': 'timeout',
            'clamp-boundary': 'survived', 'clamp-upper-gone': 'survived',
        }
        check(results == expected, f'statuses {results} == {expected}')
        check((toy / 'calc.py').read_text(encoding='utf-8') == CALC_2, 'the working tree was never mutated')
        check((run / 'tree' / 'calc.py').read_text(encoding='utf-8') == CALC_2, 'the tree is put back after every mutant')

        # a filter that matches no test: the baseline stops the run, and without the baseline the mutant is invalid
        (run / 'good.txt').write_text(MUTANTS, encoding='utf-8')
        (run / 'mutants.txt').write_text(NOTHING_RAN, encoding='utf-8')
        out = mt('run', '--run', 't1', expect=2)
        check('the baseline is not green' in out and 'no test ran' in out, 'an empty filter stops the run at the baseline')
        out = mt('run', '--run', 't1', '--no-baseline')
        check('nothing-ran: invalid' in out and 'no test ran' in out, 'and without the baseline its mutant is invalid, not caught or survived')
        (run / 'mutants.txt').write_text(MUTANTS, encoding='utf-8')

        # a killed run: a journal and a mutated file, put back by the next run
        original = (run / 'tree' / 'calc.py').read_bytes()
        (run / 'inflight-results.orig').write_bytes(original)
        (run / 'inflight-results.json').write_text(json.dumps({'file': 'calc.py', 'name': 'x', 'backup': str(run / 'inflight-results.orig')}), encoding='utf-8')
        (run / 'tree' / 'calc.py').write_text('broken = (\n', encoding='utf-8')
        out = mt('run', '--run', 't1', '--only', 'clamp-lower-wrong')
        check('restored calc.py' in out, 'a mutated file that a killed run left is restored first')
        check((run / 'tree' / 'calc.py').read_bytes() == original, 'and it is the original')

        # a new test closes the survivors; the equivalent one stays
        (toy / 'test_more.py').write_text(TEST_MORE, encoding='utf-8')
        out = mt('tree', 'sync', '--run', 't1')
        check((run / 'tree' / 'test_more.py').exists(), 'tree sync copies the new test into the tree')
        mt('run', '--run', 't1', '--only', 'clamp-upper-wrong', 'clamp-upper-gone', 'clamp-boundary')
        results = {}
        for line in (run / 'results.jsonl').read_text(encoding='utf-8').splitlines():
            entry = json.loads(line)
            results[entry['name']] = entry['status']
        check(results['clamp-upper-wrong'] == 'caught' and results['clamp-upper-gone'] == 'caught', 'the new test catches the survivors')
        check(results['clamp-boundary'] == 'survived', 'the equivalent mutant still survives')

        (run / 'dispositions.txt').write_text('clamp-boundary: equivalent  x == lo gives lo either way\n', encoding='utf-8')
        report = mt('report', '--run', 't1')
        check('Survived first, caught after a new test | 2' in report, 'the report counts the survivors a test closed')
        check('equivalent 1' in report and 'Still surviving, unexplained | 0' in report, 'the report counts the explained survivor')
        check('clamp-hangs: timeout' in report and 'clamp-no-syntax: invalid' in report, 'the report lists what has no answer')

        # parallel workers share the results through their own log names
        out = mt('run', '--run', 't1', '--redo', '--only', 'clamp-lower-wrong', '--log', 'results-2.jsonl', '--no-baseline')
        check((run / 'results-2.jsonl').exists(), 'a worker writes its own results file')
        out = mt('run', '--run', 't1', '--redo', '--part', '1/2', '--log', 'results-3.jsonl', '--no-baseline')
        check('running 3 mutants' in out or 'running 4 mutants' in out, '--part takes every second mutant')

        mt('tree', 'remove', '--run', 't1')
        check(not run.exists(), 'tree remove deletes the run')
        check('t1' not in sh('git', 'worktree', 'list', cwd=toy), 'and the worktree')

        # the working tree as a scope
        (toy / 'calc.py').write_text(CALC_2 + '\n\ndef double(x):\n    return x * 2\n', encoding='utf-8')
        out = mt('scope', '--working-tree', '--code')
        check('calc.py' in out, 'scope --working-tree lists uncommitted changes')
        mt('tree', 'create', '--run', 't2', '--working-tree')
        check('def double' in (toy / '.tmp' / 'mutation-test' / 't2' / 'tree' / 'calc.py').read_text(encoding='utf-8'), 'tree create --working-tree brings the uncommitted code')
        mt('tree', 'remove', '--run', 't2')
    finally:
        if not keep:
            shutil.rmtree(folder, onerror=writable)
    print()
    if failures:
        print(f'{len(failures)} checks failed')
        return 1
    print('all checks passed')
    return 0


if __name__ == '__main__':
    sys.exit(main())
