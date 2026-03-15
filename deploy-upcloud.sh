#!/bin/bash
set -e

VM_HOST="root@212.147.235.100"
VM_PATH="/root"
DB_PATH="SaksAppWeb/db"

echo "=== Deploying SaksApp ==="

# 1. Save and transfer Docker image
echo "Saving Docker image..."
docker save saksappweb:latest -o saksappweb.tar

#echo "Transferring image to VM..."
scp saksappweb.tar ${VM_HOST}:${VM_PATH}/

# 2. Transfer docker-compose.yml
echo "Transferring docker-compose.yml..."
scp docker-compose.yml ${VM_HOST}:${VM_PATH}/

# 3. Transfer database files
#echo "Transferring database..."
#ssh ${VM_HOST} mkdir -p ${VM_PATH}/${DB_PATH}/
#scp ${DB_PATH}/app.db* ${VM_HOST}:${VM_PATH}/${DB_PATH}/

# 4. Transfer auth config
#echo "Transferring auth config..."
#ssh ${VM_HOST} mkdir -p ${VM_PATH}/auth/
#scp auth/secret.env ${VM_HOST}:${VM_PATH}/auth/

# 5. Load and start on VM
echo "Loading image and starting on VM..."
ssh ${VM_HOST} "cd ${VM_PATH} && docker load -i saksappweb.tar && docker compose up -d --force-recreate"

echo "=== Deployment complete ==="
ssh ${VM_HOST} "docker compose ps"

#ssh ${VM_HOST} "docker logs -tf"
