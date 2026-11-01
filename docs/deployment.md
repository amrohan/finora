# Finora Deployment & Operations Guide

This guide describes the production deployment topology, Supabase database connectivity, Caddy reverse proxy setup, migration workflows, and automated backup procedures for Finora.

---

## 1. Supabase PostgreSQL Setup & Connectivity

Finora uses Supabase exclusively as a managed PostgreSQL provider.

### Connection Configuration
- **Transaction Pooler (PgBouncer)**: Use Supabase's transaction pooler (port 6543) for application API connections to prevent connection starvation:
  ```text
  Host=aws-0-ap-south-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.<project-ref>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true;
  ```
- **Direct Connection (Port 5432)**: Use direct connection strictly for running EF Core schema migrations (since PgBouncer does not support prepared statements or transactional DDL migrations).

---

## 2. VPS Deployment Topology (Single VPS)

Finora is deployed to a Linux VPS (Ubuntu 24.04 LTS, 2 vCPU, 4GB RAM) using Docker Compose and Caddy.

```text
/opt/finora/
├── docker-compose.yml
├── .env
├── caddy/
│   └── Caddyfile
├── backups/
└── scripts/
    ├── backup.sh
    └── restore.sh
```

### Docker Compose Architecture (`docker-compose.yml`)
```yaml
services:
  caddy:
    image: caddy:2-alpine
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
      - "443:443/udp" # HTTP/3 QUIC
    volumes:
      - ./caddy/Caddyfile:/etc/caddy/Caddyfile:ro
      - ./dist/frontend:/var/www/finora:ro
      - caddy_data:/data
      - caddy_config:/config
    depends_on:
      - api

  api:
    image: ghcr.io/your-username/finora-api:latest
    restart: unless-stopped
    expose:
      - "5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5000
      - DATABASE_CONNECTION_STRING=${DATABASE_CONNECTION_STRING}
      - JWT_SECRET=${JWT_SECRET}
      - JWT_ISSUER=${JWT_ISSUER}
      - JWT_AUDIENCE=${JWT_AUDIENCE}
      - ALLOWED_ORIGINS=${ALLOWED_ORIGINS}
      - INITIAL_SETUP_TOKEN=${INITIAL_SETUP_TOKEN}
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 15s
      timeout: 5s
      retries: 3

volumes:
  caddy_data:
  caddy_config:
```

---

## 3. Caddy Reverse Proxy Configuration (`Caddyfile`)

Caddy automates HTTPS certificate provisioning via Let's Encrypt and provides high-performance static asset caching:

```caddyfile
app.finora.internal, yourdomain.com {
    encode zstd gzip

    # Security Headers
    header {
        Strict-Transport-Security "max-age=31536000; includeSubDomains; preload"
        X-Content-Type-Options "nosniff"
        X-Frame-Options "DENY"
        Referrer-Policy "strict-origin-when-cross-origin"
        Permissions-Policy "camera=(), microphone=(), geolocation=()"
    }

    # API Reverse Proxy
    handle /api/* {
        reverse_proxy api:5000 {
            header_up X-Real-IP {remote_host}
            header_up X-Forwarded-For {remote_host}
            header_up X-Forwarded-Proto {scheme}
        }
    }

    # Health Check
    handle /health {
        reverse_proxy api:5000
    }

    # Static Angular SPA & PWA
    handle {
        root * /var/www/finora/browser
        try_files {path} /index.html
        file_server

        # Immutable cache for hashed assets
        @static path *.js *.css *.woff2 *.png *.svg *.ico
        header @static Cache-Control "public, max-age=31536000, immutable"

        # No cache for index.html and service worker
        @nocache path /index.html /ngsw.json /ngsw-worker.js
        header @nocache Cache-Control "no-cache, no-store, must-revalidate"
    }
}
```

---

## 4. Environment Variables Checklist

Ensure these variables are securely populated in `/opt/finora/.env`:

| Variable | Description |
| :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | Set to `Production`. |
| `DATABASE_CONNECTION_STRING` | Supabase pooled connection string with SSL enabled. |
| `JWT_SECRET` | 64-character high-entropy secret. |
| `JWT_ISSUER` | Token issuer URL (e.g. `https://api.yourdomain.com`). |
| `JWT_AUDIENCE` | Token audience (e.g. `https://yourdomain.com`). |
| `ALLOWED_ORIGINS` | Permitted frontend origins for CORS. |
| `INITIAL_SETUP_TOKEN` | One-time token used to register the initial system administrator. |
| `BACKUP_ENCRYPTION_PASSPHRASE` | Strong encryption key for database dumps. |

---

## 5. Database Migrations Workflow

Migrations are generated using the EF Core CLI in development and applied during deployment:

### In Development
```bash
# Add a new migration
dotnet ef migrations add AddAccountsAndTransactions \
    --project src/Finora.Infrastructure \
    --startup-project src/Finora.Api
```

### In Production Deployment
To ensure zero risk of migration failures inside application containers:
1. Apply migrations via an ephemeral container before rolling the API container:
   ```bash
   docker run --rm \
     -e DATABASE_CONNECTION_STRING="$DIRECT_DB_CONNECTION_STRING" \
     ghcr.io/your-username/finora-migrator:latest
   ```
2. Or use EF Core migration bundles (`bundle.exe`) executed by the CI/CD deployment runner.

---

## 6. Backup & Restore Runbook

### Automated Encrypted Backup (`scripts/backup.sh`)
```bash
#!/bin/bash
set -euo pipefail

BACKUP_DIR="/opt/finora/backups"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_FILE="${BACKUP_DIR}/finora_db_${TIMESTAMP}.sql.gz.enc"

mkdir -p "$BACKUP_DIR"

# Perform dump, gzip, and encrypt with AES-256
pg_dump "$DIRECT_DB_CONNECTION_STRING" --no-owner --no-privileges \
    | gzip \
    | openssl enc -aes-256-cbc -salt -pbkdf2 -pass env:BACKUP_ENCRYPTION_PASSPHRASE \
    -out "$BACKUP_FILE"

# Keep last 30 days of backups
find "$BACKUP_DIR" -type f -name "*.sql.gz.enc" -mtime +30 -delete

echo "Backup created successfully: $BACKUP_FILE"
```

### Restore Runbook (`scripts/restore.sh`)
```bash
#!/bin/bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
    echo "Usage: $0 <path-to-encrypted-backup-file>"
    exit 1
fi

ENCRYPTED_FILE="$1"

echo "Decrypting and restoring $ENCRYPTED_FILE..."

openssl enc -d -aes-256-cbc -pbkdf2 -pass env:BACKUP_ENCRYPTION_PASSPHRASE -in "$ENCRYPTED_FILE" \
    | gunzip \
    | psql "$DIRECT_DB_CONNECTION_STRING"

echo "Database restored successfully."
```

---

## 7. CI/CD Pipeline (GitHub Actions)

A single `.github/workflows/ci-cd.yml` workflow automates:
1. **Lint & Test**: Runs .NET unit tests and Angular test suite.
2. **Build Containers**: Builds multi-stage Docker image for the .NET API.
3. **Build Frontend**: Compiles Angular SPA in production mode with PWA service worker.
4. **Deploy to VPS**: Deploys via SSH, performs DB migration, and updates Caddy static assets with zero downtime.

