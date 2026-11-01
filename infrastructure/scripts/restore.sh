#!/usr/bin/env bash
set -euo pipefail

# Finora Production Database Restore Script
# Decrypts AES-256-CBC encrypted backup and restores schema/data to PostgreSQL.

if [ "$#" -lt 1 ]; then
  echo "Usage: $0 <path_to_backup_file.sql.gz.enc>"
  echo "Example: $0 /opt/finora/backups/finora_db_20261101_120000.sql.gz.enc"
  exit 1
fi

BACKUP_FILE="$1"

if [ ! -f "${BACKUP_FILE}" ]; then
  echo "Error: Backup file not found: ${BACKUP_FILE}" >&2
  exit 1
fi

if [ -z "${DATABASE_CONNECTION_STRING:-}" ]; then
  echo "Error: DATABASE_CONNECTION_STRING environment variable is required." >&2
  exit 1
fi

if [ -z "${BACKUP_ENCRYPTION_PASSPHRASE:-}" ]; then
  echo "Error: BACKUP_ENCRYPTION_PASSPHRASE environment variable is required." >&2
  exit 1
fi

echo "=========================================================="
echo "⚠️  WARNING: You are about to restore the database!"
echo "Target backup: ${BACKUP_FILE}"
echo "This will overwrite existing records matching the backup."
echo "=========================================================="

read -r -p "Are you sure you want to proceed? [y/N] " confirmation
if [[ ! "${confirmation}" =~ ^[Yy]$ ]]; then
  echo "Restore cancelled by user."
  exit 0
fi

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Decrypting and restoring database..."

openssl enc -d -aes-256-cbc -pbkdf2 -iter 100000 -pass env:BACKUP_ENCRYPTION_PASSPHRASE -in "${BACKUP_FILE}" \
  | gzip -d \
  | psql "${DATABASE_CONNECTION_STRING}" --single-transaction

echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] Database restore completed successfully."

