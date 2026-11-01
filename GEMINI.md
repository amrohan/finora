# GEMINI.md: Antigravity Agent Instructions for Finora

Welcome to the **Finora** codebase. This file provides your direct operational instructions, architecture boundaries, and execution rules.

---

## 1. Product Overview

**Finora** is a personal and family wealth management web application and PWA designed to give families complete financial clarity, control, and peace of mind. Finora supports multi-member household tenancy with privacy-aware visibility levels (`Household`, `Private`, `SelectedMembers`), accurate transaction-based accounting, debt management, goal tracking, and future asset tracking.

- **Primary Currency**: INR (Indian Rupee), stored internally as integer paise (`BIGINT`).
- **Timezone**: `Asia/Kolkata`.
- **Target User**: Multi-member Indian households managing shared expenses alongside personal privacy.

---

## 2. Technology Stack

- **Frontend**: Angular 20+ (100% Standalone Components, Signals, RxJS, Reactive Forms).
- **UI Components & Styling**: NG-ZORRO (`ng-zorro-antd`) + Tailwind CSS 4.
- **Backend**: .NET 10 (C# 13) Web API.
- **Data Access & ORM**: Entity Framework Core 10 (`Npgsql.EntityFrameworkCore.PostgreSQL`).
- **Database**: Supabase Managed PostgreSQL.
- **Authentication**: ASP.NET Core Identity (Cookie and Bearer Token authentication). **Do NOT use Supabase Auth.**
- **Deployment**: Single VPS with Docker Compose and Caddy reverse proxy with automatic SSL.

---

## 3. Project Architecture & Boundaries

Finora is structured as a **Modular Monolith** with clean boundaries:

```text
finora/
├── frontend/                     # Angular 20+ SPA & PWA
│   └── src/app/
│       ├── core/                 # Auth interceptors, session, API client
│       ├── shared/               # Reusable UI directives, pipes, common dialogs
│       └── features/             # Business modules (accounts, transactions, budgets...)
│
├── backend/                      # .NET 10 Solution
│   ├── Finora.Domain/            # Entities, Enums, Value Objects, Domain Exceptions
│   ├── Finora.Application/       # DTOs, Service Interfaces, Business Logic, Validators
│   ├── Finora.Infrastructure/    # EF Core DbContext, Identity, Migrations, Postgres
│   └── Finora.Api/               # Controllers, Middleware, Configuration
│
├── docs/                         # Authoritative architectural documentation
├── infrastructure/               # Docker, Caddy, deployment scripts, Supabase configs
└── .agents/                      # Ponytail methodology rules and skills
```

### Frontend vs. Backend Boundaries
- **Backend API**: The single source of truth for business invariants, validation, authorization, and financial math.
- **Frontend SPA**: A reactive, accessible client for presentation and workflow. **Never** trust the client to compute interest, aggregate net worth, or authorize records.
- **Data Exchange**: All communication occurs via typed REST DTOs over HTTPS. Domain entities must never cross the API boundary.

---

## 4. Coding Conventions

- **Ponytail Minimalism**: Default to `FULL` mode. Follow the Seven-Rung Ladder before writing code. Keep diffs focused and minimal.
- **No Abstraction Bloat**: Do not author generic repositories (`IRepository<T>`), unit-of-work abstractions, or CQRS/MediatR layers when standard services and `DbContext` queries are cleaner.
- **Async Everywhere**: All backend I/O operations must be `async` and accept a `CancellationToken`.
- **Validation**: Enforce request validation using `FluentValidation` on the backend and Angular `Reactive Forms` on the client.
- **Angular UI**: Always check NG-ZORRO components before writing custom HTML/CSS controls.

---

## 5. Financial Correctness Standards

1. **No Floating-Point Math**: Never use `float` or `double`. Use integer paise (`BIGINT` / `long`) for all INR amounts.
2. **Transfers ≠ Expenses**: Account transfers move money between accounts; they never count toward household expenditure or income totals.
3. **Credit Card Payments**: Settling a credit card balance is an account transfer from bank to credit card liability; it must never duplicate original expense entries.
4. **Loan Split**: Separate principal amortization from interest expenditure.
5. **Atomic Transactions**: All mutations affecting multiple records (transfers, splits, balance updates) must execute inside an explicit database transaction.
6. **Privacy Enforcement**: Private transactions and accounts must never appear in shared household aggregations, summaries, or exports unless the requesting user is the owner or explicitly granted access.

---

## 6. Security Requirements

- **Never Expose Supabase Credentials**: Frontend must only connect to the Finora .NET API. Frontend must never hold Supabase service role keys or direct database credentials.
- **Household Tenancy Authorization**: Never trust `householdId` from client payloads. The server must verify that the authenticated user belongs to the requested household and has sufficient role permissions (`Owner`, `Admin`, `Member`).
- **Record Visibility**: Enforce visibility rules at the query level using EF Core global query filters and explicit service predicates.
- **Audit Logging**: Write audit records for all authentication events, financial modifications, and household invitations.

---

## 7. Database Conventions

- All table and column names in PostgreSQL must be `snake_case`.
- Primary keys must be `UUID` (`gen_random_uuid()`).
- All monetary values stored as `BIGINT` (paise).
- Timestamps stored as `TIMESTAMPTZ` in UTC.
- Financial dates stored as `DATE`.
- Foreign keys must include `ON DELETE RESTRICT` or `CASCADE` intentionally, with composite indexes on `(household_id, ...)` for performant tenant scoping.
- All schema alterations must be created and reviewed via EF Core Migrations.

---

## 8. Implementation Workflow (Agent Directives)

As an AI agent operating in this codebase, you must follow this strict workflow:

1. **Read Before Writing**: Always read the relevant documentation in `docs/` (`architecture.md`, `database-schema.md`, `decisions.md`, etc.) before implementing or modifying any feature.
2. **Inspect Existing Code**: Always check existing files and components before introducing new code to avoid duplication.
3. **One Phase at a Time**: **Do NOT implement multiple development phases in a single task.** Focus exclusively on the current active milestone.
4. **Apply Ponytail First**: Question every new line, file, and dependency. Seek the smallest correct solution.
5. **Verify and Test**: Never claim a test passes without executing it. Run tests and verify builds before declaring completion.
6. **Structured Reporting**: End every response with the standard completion report (Files Changed, Features Implemented, Tests Executed, Security Considerations, Deferred Work, Technical Debt).

