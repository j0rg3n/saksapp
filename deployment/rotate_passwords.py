#!/usr/bin/env python3
"""Rotate the passwords of SaksApp users.

Generates a new random password per user, stores it as an ASP.NET Core Identity v3 hash
(PBKDF2-HMAC-SHA512, 100000 iterations, 16-byte salt, 32-byte key — the format the app's
default PasswordHasher writes and accepts) and renews SecurityStamp so existing sessions are
invalidated.

Run it on the host that owns the database, with the app stopped (docker compose stop saksappweb),
then start the app again. Plaintext passwords are written only to --output, never to stdout.
--output must be outside the repository; hand the passwords to users over a private channel and
delete the file afterwards.

  deployment/rotate_passwords.py --db ~/saksapp-data/db/app.db --output ~/rotated-passwords.tsv
  deployment/rotate_passwords.py --db ... --output ... --email a@example.com   # only some users
  deployment/rotate_passwords.py --db ... --output ... --dry-run               # change nothing
  deployment/rotate_passwords.py --self-test
"""
import argparse
import base64
import hashlib
import hmac
import os
import secrets
import sqlite3
import struct
import sys
import uuid
from datetime import datetime
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
PRF_HMAC_SHA512 = 2
ITERATIONS = 100_000
SALT_BYTES = 16
KEY_BYTES = 32

LOWER = "abcdefghijkmnopqrstuvwxyz"       # no l
UPPER = "ABCDEFGHJKLMNPQRSTUVWXYZ"        # no I, O
DIGITS = "23456789"                       # no 0, 1
SYMBOLS = "-_.!#"
ALPHABET = LOWER + UPPER + DIGITS + SYMBOLS


def generate_password(length: int = 20) -> str:
    """Random password containing every character class, avoiding look-alike characters."""
    if length < 12:
        raise ValueError("length must be at least 12")
    while True:
        pw = "".join(secrets.choice(ALPHABET) for _ in range(length))
        if (any(c in LOWER for c in pw) and any(c in UPPER for c in pw)
                and any(c in DIGITS for c in pw) and any(c in SYMBOLS for c in pw)):
            return pw


def hash_password(password: str) -> str:
    salt = secrets.token_bytes(SALT_BYTES)
    key = hashlib.pbkdf2_hmac("sha512", password.encode("utf-8"), salt, ITERATIONS, KEY_BYTES)
    blob = b"\x01" + struct.pack(">III", PRF_HMAC_SHA512, ITERATIONS, SALT_BYTES) + salt + key
    return base64.b64encode(blob).decode("ascii")


def verify_password(password: str, hashed: str) -> bool:
    blob = base64.b64decode(hashed)
    marker, prf, iterations, salt_len = struct.unpack(">BIII", blob[:13])
    if marker != 1 or prf != PRF_HMAC_SHA512:
        raise ValueError("unsupported hash format")
    salt, key = blob[13:13 + salt_len], blob[13 + salt_len:]
    actual = hashlib.pbkdf2_hmac("sha512", password.encode("utf-8"), salt, iterations, len(key))
    return hmac.compare_digest(actual, key)


def self_test() -> None:
    pw = generate_password()
    h = hash_password(pw)
    assert verify_password(pw, h)
    assert not verify_password(pw + "x", h)
    blob = base64.b64decode(h)
    assert blob[0] == 1 and len(blob) == 13 + SALT_BYTES + KEY_BYTES
    assert struct.unpack(">III", blob[1:13]) == (PRF_HMAC_SHA512, ITERATIONS, SALT_BYTES)
    assert len({generate_password() for _ in range(50)}) == 50
    print("self-test ok")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--db", help="path to the SQLite database (app.db)")
    ap.add_argument("--output", help="file to write email/name/password rows to (mode 0600, must not exist)")
    ap.add_argument("--email", action="append", help="rotate only this user (repeatable); default: all users")
    ap.add_argument("--length", type=int, default=20)
    ap.add_argument("--dry-run", action="store_true", help="list affected users, change nothing")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()

    if args.self_test:
        self_test()
        return 0
    if not args.db or not args.output:
        ap.error("--db and --output are required")

    db_path = Path(args.db).expanduser().resolve()
    out_path = Path(args.output).expanduser().resolve()
    if not db_path.is_file():
        sys.exit(f"database not found: {db_path}")
    if out_path == REPO_ROOT or REPO_ROOT in out_path.parents:
        sys.exit("refusing to write passwords inside the repository; pick a path outside it")
    if out_path.exists():
        sys.exit(f"{out_path} already exists; refusing to overwrite")

    conn = sqlite3.connect(db_path, timeout=30)
    conn.execute("PRAGMA busy_timeout = 30000")
    users = conn.execute('SELECT Id, Email, FullName FROM AspNetUsers ORDER BY Email').fetchall()
    if args.email:
        wanted = {e.lower() for e in args.email}
        users = [u for u in users if (u[1] or "").lower() in wanted]
        missing = wanted - {(u[1] or "").lower() for u in users}
        if missing:
            sys.exit(f"no such user(s): {', '.join(sorted(missing))}")
    if not users:
        sys.exit("no users to rotate")

    print(f"{len(users)} user(s) selected" + (" (dry run)" if args.dry_run else ""))
    if args.dry_run:
        for _, email, _ in users:
            print(f"  would rotate {email}")
        return 0

    backup_path = db_path.with_name(f"{db_path.name}.pre-rotate-{datetime.now():%Y%m%d-%H%M%S}.bak")
    with sqlite3.connect(backup_path) as dst:
        conn.backup(dst)
    os.chmod(backup_path, 0o600)
    print(f"database backed up to {backup_path}")

    rows = []
    with conn:  # one transaction: all users rotate or none do
        for user_id, email, full_name in users:
            pw = generate_password(args.length)
            conn.execute(
                "UPDATE AspNetUsers SET PasswordHash = ?, SecurityStamp = ?, ConcurrencyStamp = ? WHERE Id = ?",
                (hash_password(pw), str(uuid.uuid4()).upper(), str(uuid.uuid4()), user_id))
            rows.append((email or "", full_name or "", pw))
    conn.close()

    fd = os.open(out_path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as f:
        f.write("email\tname\tpassword\n")
        for r in rows:
            f.write("\t".join(r) + "\n")
    print(f"rotated {len(rows)} password(s); written to {out_path} (mode 0600)")
    print("Start the app again, distribute the passwords privately, then delete that file and the .bak backup.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
