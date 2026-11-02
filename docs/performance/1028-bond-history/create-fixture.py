"""Copy a frozen, closed SQLite seed and add one long-held bond atomically.

Uses Python's SQLite standard library to replace the retained experimental builder.
The source is read only; an existing target is always rejected. No sidecar deletion.
"""
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import sys
import tempfile


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sidecars(path):
    return [str(path) + suffix for suffix in ('-wal', '-shm', '-journal')
            if Path(str(path) + suffix).exists()]


def create(source, target, expected):
    if target.exists() or sidecars(target):
        raise ValueError('Target or its sidecars already exist')
    if sha(source) != expected or sidecars(source):
        raise ValueError('Source hash/closed-database precondition failed')
    target.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='bond-fixture-', dir=target.parent) as work:
        staging = Path(work) / 'seed.sqlite'
        # VACUUM INTO from an immutable read-only connection creates a standalone DELETE-mode file,
        # instead of copying the WAL-mode header and producing an orphan SHM on this SQLite build.
        source_connection = sqlite3.connect(source.as_uri() + '?mode=ro&immutable=1', uri=True)
        try:
            source_connection.execute('VACUUM INTO ?', (str(staging),)).fetchall()
        finally:
            source_connection.close()
        con = sqlite3.connect(staging)
        try:
            # Switch BEFORE committing fixture rows, consume the result, then close all handles.
            # This avoids the prior builder's post-commit WAL/SHM cleanup failure.
            if con.execute('PRAGMA journal_mode=DELETE').fetchone()[0] != 'delete':
                raise RuntimeError('DELETE journal mode required')
            con.execute('PRAGMA foreign_keys=ON')
            with con:
                marker = 'E01 synthetic long-held bond'
                if con.execute('SELECT 1 FROM Accounts WHERE Name=?', (marker,)).fetchone():
                    raise ValueError('Source already contains the fixture')
                user, currency, label = con.execute(
                    'SELECT UserId, CurrencyId, AccountLabel FROM Accounts WHERE AccountId=4 AND AccountType=2').fetchone()
                start_s, end_s = con.execute(
                    'SELECT min(PostingDate), max(PostingDate) FROM CurrencyEntries WHERE AccountId=1').fetchone()
                start, end = (dt.datetime.fromisoformat(s).date() for s in (start_s, end_s))
                posting = start - dt.timedelta(days=3650)
                account = con.execute('INSERT INTO Accounts(AccountType,AccountLabel,UserId,Name,CurrencyId) VALUES (2,?,?,?,?)',
                                      (label, user, marker, currency)).lastrowid
                bond = con.execute('INSERT INTO Bonds(Name,Issuer,StartEmissionDate,EndEmissionDate,Type,CurrencyId,UnitValue) VALUES (?,?,?,?,0,?,?)',
                                   (marker, marker, posting.isoformat(), end.isoformat(), currency, '100.0')).lastrowid
                con.execute('INSERT INTO BondCalculationMethod(BondDetailsId,DateOperator,DateValue,Rate) VALUES (?,0,?,?)',
                            (bond, end.isoformat(), '0.05'))
                entry = con.execute('INSERT INTO BondEntries(BondDetailsId,AccountId,PostingDate,Value,ValueChange) VALUES (?,?,?,?,?)',
                                    (bond, account, posting.isoformat() + ' 00:00:00', '1.0', '1.0')).lastrowid
                if con.execute('PRAGMA foreign_key_check').fetchall():
                    raise RuntimeError('Foreign key check failed')
            if con.execute('PRAGMA integrity_check').fetchone()[0] != 'ok':
                raise RuntimeError('Integrity check failed')
        finally:
            con.close()
        if sidecars(staging) or sidecars(source) or sha(source) != expected:
            raise RuntimeError('Closed-database/source preservation postcondition failed')
        # Exclusive publication: never overwrite another fixture.
        os.link(staging, target)
    print(json.dumps(dict(source_sha256=expected, fixture_sha256=sha(target), account_id=account,
                          bond_id=bond, entry_id=entry, start=start_s, end=end_s,
                          posting=posting.isoformat(), age_days=3650), indent=2))


if __name__ == '__main__':
    if len(sys.argv) != 4:
        raise SystemExit('Usage: create-fixture.py SOURCE TARGET EXPECTED_SOURCE_SHA256')
    create(Path(sys.argv[1]).resolve(), Path(sys.argv[2]).resolve(), sys.argv[3])
