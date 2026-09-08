#!/bin/bash
set -e

source "$(dirname "$0")/../auth/secret.env"

VM_HOST="root@${UPCLOUD__HOST}"
VM_PATH="/root"

echo "=== Deploying SaksApp ==="
echo "Target: ${VM_HOST}:${VM_PATH}"

# 1. Save and transfer Docker image
echo "Saving Docker image..."
docker save saksappweb:latest -o "$(dirname "$0")/../saksappweb.tar"

echo "Transferring image to VM..."
scp "$(dirname "$0")/../saksappweb.tar" ${VM_HOST}:${VM_PATH}/

# 2. Transfer docker-compose.yml and the proxy config
echo "Transferring docker-compose.yml and Caddyfile..."
scp "$(dirname "$0")/../docker-compose.yml" ${VM_HOST}:${VM_PATH}/
scp "$(dirname "$0")/../Caddyfile" ${VM_HOST}:${VM_PATH}/

# 2b. Sync auth/ so the compose env_file entries resolve on the VM. Previously these were placed
# by hand, which meant a fresh VM could not be brought up from this script alone.
echo "Syncing auth/ ..."
ssh ${VM_HOST} "mkdir -p ${VM_PATH}/auth"
rsync -av "$(dirname "$0")/../auth/" ${VM_HOST}:${VM_PATH}/auth/

# 3. Sync deployment folder (service file, helper scripts)
echo "Syncing deployment folder..."
rsync -av "$(dirname "$0")/" ${VM_HOST}:${VM_PATH}/deployment/

# 4. Install/update systemd service
echo "Installing systemd service..."
ssh ${VM_HOST} "cp ${VM_PATH}/deployment/saksapp.service /etc/systemd/system/ && systemctl daemon-reload && systemctl enable saksapp"

# 5. Stop service, load image, start service
echo "Loading image and restarting service..."
ssh ${VM_HOST} "systemctl stop saksapp || true"
ssh ${VM_HOST} "docker load -i ${VM_PATH}/saksappweb.tar"
ssh ${VM_HOST} "systemctl start saksapp"

# 6. Clean up. Every deploy used to leave the tarball behind AND turn the previous image into a
# dangling one — roughly 1 GB per deploy. That accumulation, not the backups, filled the 9.8 GB
# disk on 2026-09-08 and took the app down with SQLITE_READONLY on every write.
echo "Cleaning up deploy artefacts..."
ssh ${VM_HOST} "rm -f ${VM_PATH}/saksappweb.tar"
ssh ${VM_HOST} "docker image prune -f"
rm -f "$(dirname "$0")/../saksappweb.tar"

echo "=== Deployment complete ==="
ssh ${VM_HOST} "systemctl status saksapp --no-pager"
echo ""
echo "Disk headroom on the VM:"
ssh ${VM_HOST} "df -h / | tail -1"
