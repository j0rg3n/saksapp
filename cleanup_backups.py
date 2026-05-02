#!/usr/bin/env python3
import os
import re
import subprocess
from datetime import datetime, timedelta
from collections import defaultdict

BACKUP_DIR = os.path.expanduser("~/Documents/SaksApp_backups")
PATTERN = re.compile(r"app-(\d{8})-(\d{6})\.sqlite$")

def parse_backup_filename(filename):
    match = PATTERN.match(filename)
    if match:
        date_str, time_str = match.groups()
        dt = datetime.strptime(date_str + time_str, "%Y%m%d%H%M%S")
        return dt
    return None

def main():
    files = [f for f in os.listdir(BACKUP_DIR) if f.endswith(".sqlite")]
    backups = []
    
    for f in files:
        dt = parse_backup_filename(f)
        if dt:
            backups.append((dt, f))
    
    backups.sort()
    
    today = datetime.now().date()
    cutoff_daily = today - timedelta(days=14)
    cutoff_weekly = today - timedelta(days=365)
    
    to_keep = set()
    to_delete = []
    
    by_date = defaultdict(list)
    for dt, f in backups:
        by_date[dt.date()].append((dt, f))
    
    for date_key, day_backups in sorted(by_date.items()):
        if date_key >= cutoff_daily:
            keep = day_backups[0]
            to_keep.add(keep[1])
            for dt, f in day_backups[1:]:
                to_delete.append(f)
        elif date_key >= cutoff_weekly:
            week_num = (date_key - cutoff_daily).days // 7
            keep = day_backups[0]
            week_key = (week_num, date_key)
            key = (week_key, keep[1])
            if key not in to_keep:
                to_keep.add(keep[1])
            for dt, f in day_backups[1:]:
                to_delete.append(f)
        else:
            for dt, f in day_backups:
                to_delete.append(f)
    
    print(f"Total files: {len(files)}")
    print(f"Keeping: {len(to_keep)}")
    print(f"Deleting: {len(to_delete)}")
    
    for f in to_delete:
        path = os.path.join(BACKUP_DIR, f)
        try:
            os.remove(path)
            print(f"Deleted: {f}")
        except Exception as e:
            print(f"Error deleting {f}: {e}")
    
    for f in sorted(to_keep):
        path = os.path.join(BACKUP_DIR, f)
        if not path.endswith(".bz2") and not os.path.exists(path + ".bz2"):
            print(f"Compressing: {f}")
            try:
                subprocess.run(["bzip2", "-k", path], check=True)
                os.remove(path)
                print(f"  Compressed: {f}.bz2")
            except Exception as e:
                print(f"  Error compressing {f}: {e}")

if __name__ == "__main__":
    main()