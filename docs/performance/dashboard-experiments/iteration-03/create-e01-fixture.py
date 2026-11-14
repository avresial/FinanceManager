import calendar
import datetime as dt
import hashlib
import json
from pathlib import Path
import sqlite3
import sys

root = Path(__file__).resolve().parents[4]
original = root / 'code/FinanceManager.Benchmarks/bin/Release/net10.0/benchmark-data/seed-template.sqlite'
target_expected = Path(__file__).resolve().parent / 'e01-data-63ac2cb175ab4e229d8fe34ed7b5bf63/seed-template.sqlite'
expected = 'c2f4978df39ff91a80d7acbaf074099ecde224fdc0e2a4da6bd1ec24b0e37867'
marker = 'E01 synthetic long-held bond'
def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
def sidecars(path):
    return [str(path) + suffix for suffix in ('-wal', '-shm', '-journal') if Path(str(path) + suffix).exists()]
if len(sys.argv) != 2 or Path(sys.argv[1]).resolve() != target_expected:
    raise SystemExit('Pass the designated isolated template path explicitly')
target = Path(sys.argv[1]).resolve()
original_sha, input_sha = sha(original), sha(target)
if original_sha != expected or input_sha != expected or sidecars(original) or sidecars(target):
    raise SystemExit('Hash or sidecar precondition failed')
con = sqlite3.connect(target)
try:
    con.execute('PRAGMA foreign_keys=ON')
    with con:
        if con.execute('SELECT 1 FROM Accounts WHERE Name=?', (marker,)).fetchone():
            raise RuntimeError('Marker account already exists')
        user, currency, label = con.execute('SELECT UserId, CurrencyId, AccountLabel FROM Accounts WHERE AccountId=4 AND AccountType=2').fetchone()
        start_s, end_s = con.execute('SELECT min(PostingDate), max(PostingDate) FROM CurrencyEntries WHERE AccountId=1').fetchone()
        start, end = (dt.datetime.fromisoformat(s).date() for s in (start_s, end_s))
        month = end.month - 1 or 12
        year = end.year - (end.month == 1)
        last_month = max(start, dt.date(year, month, min(end.day, calendar.monthrange(year, month)[1])))
        posting = start - dt.timedelta(days=3650)
        account = con.execute('INSERT INTO Accounts(AccountType,AccountLabel,UserId,Name,CurrencyId) VALUES (2,?,?,?,?)', (label,user,marker,currency)).lastrowid
        bond = con.execute('INSERT INTO Bonds(Name,Issuer,StartEmissionDate,EndEmissionDate,Type,CurrencyId,UnitValue) VALUES (?,?,?,?,0,?,?)', (marker,marker,posting.isoformat(),end.isoformat(),currency,'100.0')).lastrowid
        method = con.execute('INSERT INTO BondCalculationMethod(BondDetailsId,DateOperator,DateValue,Rate) VALUES (?,0,?,?)', (bond,end.isoformat(),'0.05')).lastrowid
        entry = con.execute('INSERT INTO BondEntries(BondDetailsId,AccountId,PostingDate,Value,ValueChange) VALUES (?,?,?,?,?)', (bond,account,posting.isoformat()+' 00:00:00','1.0','1.0')).lastrowid
        if con.execute('PRAGMA foreign_key_check').fetchall():
            raise RuntimeError('Foreign key check failed')
    counts = {t: con.execute('SELECT count(*) FROM '+t).fetchone()[0] for t in ('Accounts','Bonds','BondEntries','BondCalculationMethod')}
    con.execute('PRAGMA wal_checkpoint(TRUNCATE)').fetchall()
    if con.execute('PRAGMA journal_mode=DELETE').fetchone()[0] != 'delete':
        raise RuntimeError('Could not switch to DELETE journal mode')
finally:
    con.close()
# A closed DELETE-mode database has no WAL; SQLite may leave its shared-memory file.
shm = Path(str(target) + '-shm')
if shm.exists() and not Path(str(target) + '-wal').exists():
    shm.unlink()
if sidecars(target) or sidecars(original) or sha(original) != expected:
    raise RuntimeError('Postcondition failed')
print(json.dumps(dict(original_sha=original_sha,input_sha=input_sha,final_sha=sha(target),account_id=account,bond_id=bond,method_id=method,entry_id=entry,user_id=user,currency_id=currency,account_type=2,posting_utc=posting.isoformat()+'T00:00:00Z',start_utc=start_s.replace(' ','T')+'Z',end_utc=end_s.replace(' ','T')+'Z',last_month_start_utc=last_month.isoformat()+'T00:00:00Z',age_days=(start-posting).days,method='UntilDate inclusive',method_end=end.isoformat(),rate='0.05',row_counts=counts),indent=2))
