#!/bin/bash
# Sync SaksApp backups off the VM, compress and deduplicate them, and keep the local archive
# bounded. Historically this script only synced and trimmed the remote, so the local archive grew
# without limit (34 GB / 2324 files before this was fixed).
#
# Env overrides: LOCAL_PATH, KEEP_REMOTE, KEEP_LOCAL, MAX_LOCAL_BYTES, DRY_RUN=1
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/../auth/secret.env"

VM_HOST="root@${UPCLOUD__HOST}"
VM_PATH="/root/SaksAppWeb/db/Backups"

# Resolved from the script's own location, not the caller's cwd: the old relative "../SaksApp_backups"
# only landed in the right place when run from the repo root.
LOCAL_PATH="${LOCAL_PATH:-$(cd "$SCRIPT_DIR/../.." && pwd)/SaksApp_backups}"

KEEP_REMOTE="${KEEP_REMOTE:-5}"
KEEP_LOCAL="${KEEP_LOCAL:-400}"
MAX_LOCAL_BYTES="${MAX_LOCAL_BYTES:-$((10 * 1024 * 1024 * 1024))}"
DRY_RUN="${DRY_RUN:-0}"

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

# All formats the archive has ever held. Anything missing from this list becomes invisible to
# pruning and accumulates forever — that is how ~1 GB of .bz2 survived for months.
find_backups() {
    find "$LOCAL_PATH" -maxdepth 1 -type f \
        \( -name 'app-*.sqlite' -o -name 'app-*.sqlite.gz' -o -name 'app-*.sqlite.bz2' \) "$@"
}

archive_bytes() { find_backups -printf '%s\n' | awk '{t+=$1} END {print t+0}'; }
archive_count() { find_backups -printf '.\n' | wc -l; }
human() { numfmt --to=iec --suffix=B "${1:-0}" 2>/dev/null || echo "${1:-0} bytes"; }

mkdir -p "$LOCAL_PATH"

BEFORE_COUNT="$(archive_count)"
BEFORE_BYTES="$(archive_bytes)"

echo "=== Syncing SaksApp Backups ==="
echo "Local archive: $LOCAL_PATH"
echo "Before: $BEFORE_COUNT file(s), $(human "$BEFORE_BYTES")"
echo ""

# --ignore-existing matches on name alone, so once a downloaded app-X.sqlite has been gzipped to
# app-X.sqlite.gz the remote original looks new again and the whole archive re-downloads. Build an
# explicit exclude list of what we already hold, with the compression suffix stripped.
EXCLUDE_FILE="$TMP_DIR/exclude.txt"
find_backups -printf '%f\n' | sed -e 's/\.gz$//' -e 's/\.bz2$//' | sort -u > "$EXCLUDE_FILE"
echo "Already held locally: $(wc -l < "$EXCLUDE_FILE") snapshot(s) excluded from transfer"

rsync -az --info=stats2 --ignore-existing --exclude-from="$EXCLUDE_FILE" \
    "${VM_HOST}:${VM_PATH}/" "${LOCAL_PATH}/"

# --- Deduplicate ---------------------------------------------------------------------------
# The DB is snapshotted hourly but changes only a few times a day, so consecutive snapshots are
# frequently byte-identical. Drop a new snapshot when it matches the one before it.
mapfile -t NEW_FILES < <(find "$LOCAL_PATH" -maxdepth 1 -type f -name 'app-*.sqlite' -printf '%f\n' | sort)

PREV_HASH=""
if [ "${#NEW_FILES[@]}" -gt 0 ]; then
    # Seed with the newest snapshot already in the archive so the batch boundary is covered too.
    NEWEST_KEPT="$(find_backups -printf '%f\n' | grep -v '\.sqlite$' | sort | tail -1 || true)"
    if [ -n "$NEWEST_KEPT" ]; then
        case "$NEWEST_KEPT" in
            *.gz)  PREV_HASH="$(gzip  -dc "$LOCAL_PATH/$NEWEST_KEPT" | sha256sum | cut -d' ' -f1)" ;;
            *.bz2) PREV_HASH="$(bzip2 -dc "$LOCAL_PATH/$NEWEST_KEPT" | sha256sum | cut -d' ' -f1)" ;;
        esac
    fi
fi

DUPES=0
KEPT_NEW=0
for f in ${NEW_FILES[@]+"${NEW_FILES[@]}"}; do
    HASH="$(sha256sum "$LOCAL_PATH/$f" | cut -d' ' -f1)"
    if [ -n "$PREV_HASH" ] && [ "$HASH" = "$PREV_HASH" ]; then
        rm -f "$LOCAL_PATH/$f"
        DUPES=$((DUPES + 1))
        continue
    fi
    PREV_HASH="$HASH"
    KEPT_NEW=$((KEPT_NEW + 1))
done
echo "Downloaded: $KEPT_NEW new snapshot(s), $DUPES duplicate(s) discarded"

# --- Compress ------------------------------------------------------------------------------
# Existing .bz2 files are left alone: already compressed, nothing to gain from repacking.
find "$LOCAL_PATH" -maxdepth 1 -type f -name 'app-*.sqlite' -print0 | xargs -0 -r -P4 gzip -9
echo "Compressed new snapshots"

# --- Prune local ---------------------------------------------------------------------------
# Bounded by count AND total bytes, whichever binds first; newest is always kept. Ordered by the
# timestamp in the filename, which sorts lexicographically, not by filesystem timestamps.
echo ""
echo "Pruning local archive (keep <= $KEEP_LOCAL files, <= $(human "$MAX_LOCAL_BYTES"))..."
KEPT=0
RUNNING=0
PRUNED=0
PRUNED_BYTES=0
while IFS=$'\t' read -r size name; do
    [ -z "$name" ] && continue
    if [ "$KEPT" -gt 0 ] && { [ "$KEPT" -ge "$KEEP_LOCAL" ] || [ $((RUNNING + size)) -gt "$MAX_LOCAL_BYTES" ]; }; then
        if [ "$DRY_RUN" = "1" ]; then
            echo "  would delete $name ($(human "$size"))"
        else
            rm -f "$LOCAL_PATH/$name"
        fi
        PRUNED=$((PRUNED + 1))
        PRUNED_BYTES=$((PRUNED_BYTES + size))
        continue
    fi
    KEPT=$((KEPT + 1))
    RUNNING=$((RUNNING + size))
done < <(find_backups -printf '%s\t%f\n' | sort -t$'\t' -k2,2r)

if [ "$DRY_RUN" = "1" ]; then
    echo "DRY RUN: would prune $PRUNED file(s), reclaiming $(human "$PRUNED_BYTES")"
else
    echo "Pruned $PRUNED file(s), reclaimed $(human "$PRUNED_BYTES")"
fi

echo ""
echo "After: $(archive_count) file(s), $(human "$(archive_bytes)")"

echo ""
echo "Trimming remote backups (keeping last $KEEP_REMOTE)..."
ssh "$VM_HOST" "cd $VM_PATH && ls -1t | tail -n +$((KEEP_REMOTE + 1)) | xargs -r rm -v"

echo ""
echo "Remote backups remaining:"
ssh "$VM_HOST" "ls -1t $VM_PATH/"

# Left as-is deliberately: OS patching is unrelated to backup sync, and "apt upgrade" without -y
# will block waiting for confirmation on a non-interactive run. Worth splitting into its own
# deployment/update-vm.sh, but that is a separate decision.
ssh "$VM_HOST" "sudo apt update"
ssh "$VM_HOST" "sudo apt upgrade"
