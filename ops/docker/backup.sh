#!/bin/bash
set -euo pipefail
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
mkdir -p ./backups
docker exec -t kuyumculuktakipprogrami-db pg_dump -U ktp ktp_db > ./backups/db_backup_${TIMESTAMP}.sql
find ./backups -type f -mtime +7 -delete

