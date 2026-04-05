#!/bin/bash
set -e

VM_HOST="root@212.147.235.100"
VM_PATH="/root/SaksAppWeb/db/Backups"
LOCAL_PATH="../SaksApp_backups"
KEEP_REMOTE=5

echo "=== Syncing SaksApp Backups ==="

mkdir -p "$LOCAL_PATH"

echo "Syncing to $LOCAL_PATH/ (skipping existing files)..."
rsync -avz --ignore-existing "${VM_HOST}:${VM_PATH}/" "${LOCAL_PATH}/"

echo ""
echo "=== Sync complete ==="
echo "Syncing $LOCAL_PATH/"
echo "Total files: $(ls -1 "$LOCAL_PATH/" | wc -l)"
ls -1t "$LOCAL_PATH/" | tail -5 | while read -r backup; do
    echo "  $backup"
done
echo "..."

echo ""
echo "Trimming remote backups (keeping last $KEEP_REMOTE)..."
ssh "$VM_HOST" "cd $VM_PATH && ls -1t | tail -n +$((KEEP_REMOTE + 1)) | xargs -r rm -v"

echo ""
echo "Remote backups remaining:"
ssh "$VM_HOST" "ls -1t $VM_PATH/"
