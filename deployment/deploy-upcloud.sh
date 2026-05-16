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

# 2. Transfer docker-compose.yml
echo "Transferring docker-compose.yml..."
scp "$(dirname "$0")/../docker-compose.yml" ${VM_HOST}:${VM_PATH}/

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

echo "=== Deployment complete ==="
ssh ${VM_HOST} "systemctl status saksapp --no-pager"
