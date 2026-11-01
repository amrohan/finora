# Supabase PostgreSQL Configuration for Finora

Finora uses Supabase exclusively as a managed PostgreSQL engine. ASP.NET Core Identity is used for authentication; **Supabase Auth is intentionally not used**.

---

## 1. Connection Strings

### Application API Connection (Transaction Pooler)
Use PgBouncer (Port `6543`) with transaction pooling for the running .NET API container:
```text
Host=aws-0-ap-south-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.<project-ref>;Password=<db-password>;SSL Mode=Require;Trust Server Certificate=true;
```

### Direct Database Connection (Migrations & Backups)
Use Port `5432` for running EF Core migrations and `pg_dump` backups (since PgBouncer does not support transactional DDL or prepared statements):
```text
Host=db.<project-ref>.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<db-password>;SSL Mode=Require;Trust Server Certificate=true;
```

---

## 2. PostgreSQL Extensions

Finora relies on standard PostgreSQL extensions enabled by default on Supabase:
- `pgcrypto` (for `gen_random_uuid()` default expressions).
- `uuid-ossp` (for standard UUID v4 generation).

---

## 3. Storage Invariants

- **paise storage**: All monetary INR values are stored as `BIGINT` paise (1 INR = 100 paise).
- **Timezone**: All timestamps are `TIMESTAMPTZ` in UTC.
- **Foreign Keys**: Enforced composite keys and tenant cascades referencing `households(id)`.

