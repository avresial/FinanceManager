"""Run retained parity data and reject duplicate/missing daily points on isolated copies."""
import copy
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

here = Path(__file__).resolve().parent
raw = here / 'raw'
validator = here / 'compare-responses.py'


def validate(directory):
    return subprocess.run([sys.executable, str(validator), str(directory)], capture_output=True, text=True)


valid = validate(raw)
assert valid.returncode == 0, valid.stderr
print('Retained full/month baseline/candidate/control: PASS')
responses = {name: json.loads((raw / (name + '-response.json')).read_text())
             for name in ('baseline', 'candidate', 'control')}
cases = 0
for window in ('full', 'month'):
    targets = [('baseline',), ('candidate',), ('control',), ('baseline', 'candidate', 'control')]
    for names in targets:
        for corruption in ('duplicate-with-missing-day', 'missing-day'):
            with tempfile.TemporaryDirectory(prefix='bond-date-coverage-') as work:
                work = Path(work)
                for path in raw.glob('*.sha256'):
                    shutil.copyfile(path, work / path.name)
                data = copy.deepcopy(responses)
                for name in names:
                    series = data[name][window]['netWorthSeries']
                    if corruption == 'duplicate-with-missing-day':
                        series[1] = copy.deepcopy(series[0])
                    else:
                        del series[1]
                for name, response in data.items():
                    (work / (name + '-response.json')).write_text(json.dumps(response))
                result = validate(work)
                assert result.returncode != 0 and window + ' date coverage differs' in result.stderr, result.stderr
                cases += 1
                print(f'{window} {"+".join(names)} {corruption}: REJECTED (exit {result.returncode})')
print(f'PASS: {cases} corruptions rejected; retained artifacts untouched.')
