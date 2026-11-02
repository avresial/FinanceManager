"""Check full response parity, selection proof, every fixture contribution, and seed hashes."""
from decimal import Decimal
from datetime import datetime, timedelta
import json
from pathlib import Path
import sys

root = Path(sys.argv[1])
def read(name):
    return json.loads((root / name).read_text(), parse_float=Decimal)

def date(value):
    return datetime.fromisoformat(value.replace('Z', '+00:00'))

def expected_dates(start, end):
    start, end = date(start), date(end)
    assert start <= end
    return {end - timedelta(days=i) for i in range((end - start).days + 1)}

baseline, candidate, control = (read(name + '-response.json') for name in ('baseline', 'candidate', 'control'))
proof = candidate['proof']
assert proof['ageDays'] >= 3650
full_dates = expected_dates(candidate['Start'], candidate['End'])
assert len(proof['prices']) == proof['selectedCount'] == len(full_dates)
assert {date(day) for day in proof['prices']} == full_dates, 'proof date coverage differs'
checks = {}
for window in ('full', 'month'):
    start = candidate['Start' if window == 'full' else 'LastMonthStart']
    expected = expected_dates(start, candidate['End'])
    for name, response in (('baseline', baseline), ('candidate', candidate), ('control', control)):
        assert date(response[window]['startDate']) == date(start), name + ' window start differs'
        assert date(response[window]['endDate']) == date(candidate['End']), name + ' window end differs'
        dates = [date(point['dateTime']) for point in response[window]['netWorthSeries']]
        assert len(dates) == len(set(dates)) and set(dates) == expected, name + ' ' + window + ' date coverage differs'
    assert baseline[window] == candidate[window], window + ' complete response differs'
    fixture_series, control_series = (x[window]['netWorthSeries'] for x in (candidate, control))
    assert len(fixture_series) == len(control_series)
    for fixture, original in zip(fixture_series, control_series):
        assert fixture['dateTime'] == original['dateTime']
        price = proof['prices'][fixture['dateTime']]
        # Each aggregate is rounded independently after adding all other accounts.
        assert abs(fixture['value'] - original['value'] - price) <= Decimal('0.01')
    fixture_assets = candidate[window]['assetsPerAccount']
    fixture_account = [a for a in fixture_assets if a['accountId'] == proof['accountId']]
    assert len(fixture_account) == 1
    assert fixture_account[0]['value'] == proof['endPrice']
    assert not any(a['accountId'] == proof['accountId'] for a in control[window]['assetsPerAccount'])
    checks[window] = dict(complete_response_equal=True, exact_date_coverage=True, all_bucket_contributions_proven=len(fixture_series))

seed_hashes = [(root / (name + '.sha256')).read_text().split()[0]
               for name in ('baseline-before', 'baseline-after', 'candidate-before', 'candidate-after')]
assert len(set(seed_hashes)) == 1
assert (root / 'control-before.sha256').read_text() == (root / 'control-after.sha256').read_text()
print(json.dumps(dict(windows=checks, selected_entry=proof['entryId'], age_days=proof['ageDays'],
                      seed_sha256=seed_hashes[0], seed_preserved=True), indent=2))
