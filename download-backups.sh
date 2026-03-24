#!/bin/bash
set -e

VM_HOST="root@212.147.235.100"
VM_PATH="/root/SaksAppWeb/db/Backups"
LOCAL_PATH="./backups"

echo "=== Downloading SaksApp Backups ==="

mkdir -p "$LOCAL_PATH"

echo "Fetching backup list from VM..."
BACKUPS=$(ssh "$VM_HOST" "ls -1 $VM_PATH/")

if [ -z "$BACKUPS" ]; then
    echo "No backups found on VM."
    exit 0
fi

echo "Found backups:"
echo "$BACKUPS"
echo ""

echo "Downloading to $LOCAL_PATH/ ..."
echo "$BACKUPS" | xargs -P 8 -I {} scp "${VM_HOST}:${VM_PATH}/{}" "${LOCAL_PATH}/"

echo ""
echo "=== Download complete ==="
ls -la "$LOCAL_PATH/"
