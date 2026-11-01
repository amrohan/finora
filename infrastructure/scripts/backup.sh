#!/usr/bin/env bash
set -euo pipefail

# Finora Production Database Backup Script
# Performs compressed pg_dump and AES-256-CBC encryption using PBKDF2.

BACKUP_DIR="${BACKUP_DIR:-/opt/finora/backups}"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_FILE="${BACKUP_DIR}/finora_db_${TIMESTAMP}.sql.gz.enc"

if [ -z "${DATABASE_CONNECTION_STRING:-}" ]; then
  echo "Error: DATABASE_CONNECTION_STRING environment variable is required." >&2
  exit 1
fi

if [ -z "${BACKUP_ENCRYPTION_PASSPHRASE:-}" ]; then
  echo "Error: BACKUP_ENCRYPTION_PASSPHRASE environment variable is required." >&2
  exit 1
fi

mkdir -p "${BACKUP_DIR}"

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Starting database backup..."

# Extract connection parameters or pass URI to pg_dump
pg_dump "${DATABASE_CONNECTION_STRING}" \
  --no-owner \
  --no-privileges \
  --clean \
  --if-exists \
  | gzip -9 \
  | openssl enc -aes-256-cbc -salt -pbkdf2 -iter 100000 -pass env:BACKUP_ENCRYPTION_PASSPHRASE \
  -out "${BACKUP_FILE}"

# Set secure permissions (read/write only by owner)
chmod 600 "${BACKUP_FILE}"

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Backup successfully encrypted and saved to: ${BACKUP_FILE}"
echo "File size: $(ls -lh "${BACKUP_FILE}" | awk '{print $5}')"

# Retention policy: Prune backup archives older than 30 days
echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Pruning backups older than 30 days..."
find "${BACKUP_DIR}" -type f -name "finora_db_*.sql.gz.enc" -mtime +30 -exec rm -f {} +

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Backup maintenance complete."

