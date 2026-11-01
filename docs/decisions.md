# Architecture Decision Records (ADRs)

This document records the architectural and engineering decisions made for the Finora project, detailing the context, options considered, decisions taken, and consequences.

---

## ADR-001: PostgreSQL instead of SQLite

### Context
Finora is a multi-member family financial management system deployed to a Linux VPS, with multiple concurrent users mutating financial transactions, budgets, and accounts.

### Decision
We chose **PostgreSQL 16+** (hosted via Supabase) as the primary persistence engine over SQLite.

### Consequences
- **Advantages**:
  - Full support for concurrent write transactions without table/file locking bottlenecks (`WAL` mode).
  - Native `UUID` generation (`gen_random_uuid()`), `JSONB` for audit logs, rich date/time functions, and Row-Level Security (RLS) capability.
  - Enterprise connection pooling via Supabase's transaction pooler (PgBouncer).
  - Scalability for multi-year financial history and high-volume split transactions.
- **Trade-offs**:
  - Requires network roundtrip to managed PostgreSQL compared to in-process SQLite file access.

---

## ADR-002: Modular Monolith instead of Microservices

### Context
Family finance platforms require strong transactional integrity (e.g., transfers updating two accounts atomically, loan payments adjusting principal and recording expenses). Microservices introduce distributed transactions (Saga patterns), eventual consistency, multiple deployment targets, high network latency, and severe operational overhead for small teams.

### Decision
We architected Finora as a **Modular Monolith** in .NET 10. All modules (`Identity`, `Household`, `Finance`, `Planning`, `Wealth`, `Analytics`) execute in a single process sharing a database context while maintaining strict boundary separation.

### Consequences
- **Advantages**:
  - Atomic database transactions across multiple financial domain entities without distributed two-phase commit overhead.
  - Simple local debugging, single CI/CD pipeline, and single Docker container deployment on a cost-effective VPS.
  - Clear architectural boundaries allow future extraction into microservices if scaling ever requires it.
- **Trade-offs**:
  - Requires internal discipline to prevent boundary erosion (enforced via project references and internal interfaces).

---

## ADR-003: ASP.NET Core Identity instead of Supabase Auth / Third-Party Auth

### Context
We evaluated whether to use Supabase Auth (GoTrue) or ASP.NET Core Identity for user management, authentication, and session control.

### Decision
We chose **ASP.NET Core Identity** (Cookie and Bearer Token authentication) running natively inside the .NET backend API.

### Consequences
- **Advantages**:
  - Direct integration with EF Core DbContext, user entities, and permission checks.
  - Complete control over password hashing, refresh token rotation, session invalidation, and custom invitation flows.
  - No dependency on third-party JavaScript auth SDKs in Angular.
  - Consistent security boundary: Angular only talks to the .NET API.
- **Trade-offs**:
  - Requires maintaining identity controllers, token generation, and password recovery logic on the backend.

---

## ADR-004: Supabase Used Exclusively as Managed PostgreSQL

### Context
Supabase offers an entire ecosystem including Auth, Storage, Edge Functions, Realtime websockets, and auto-generated REST/GraphQL APIs (PostgREST).

### Decision
We use Supabase **solely as a managed PostgreSQL database provider**. We do not use Supabase Auth, Storage, PostgREST, or client SDKs.

### Consequences
- **Advantages**:
  - Eliminates client-side Supabase API key exposure.
  - Prevents dual-source-of-truth problems between Supabase Auth and application domain tables.
  - Avoids vendor lock-in: the backend can migrate to AWS RDS, Azure Database for PostgreSQL, DigitalOcean Managed PostgreSQL, or self-hosted PostgreSQL simply by altering the connection string.
- **Trade-offs**:
  - We do not use Supabase Realtime out of the box (SignalR will be used if real-time push is required).

---

## ADR-005: Transaction-Based Accounting with Ledger Mechanics

### Context
Personal finance apps often take one of two approaches:
1. *State-based*: Storing account balance as a mutable field and overwriting it directly.
2. *Transaction-based*: Calculating balances from an immutable ledger of transactions, or updating balance atomically inside transaction mutations.

### Decision
We adopted **Transaction-Based Accounting**. Every financial movement is represented by an immutable transaction record (Income, Expense, Transfer). Account balances are updated atomically within the same database transaction that records the transaction.

### Consequences
- **Advantages**:
  - Complete audit trail of all historical balance changes.
  - Prevents balance drift and unexplained discrepancies.
  - Enables historical reconstruction and point-in-time balance calculations.
  - Enforces the core accounting rule: Transfers are balance relocations, never expenses.
- **Trade-offs**:
  - Requires database transactions (`IDbContextTransaction`) for all mutations.

---

## ADR-006: Multi-Tenant Household Model with Family Scoping

### Context
Unlike individual-only finance apps or enterprise B2B SaaS, personal finance involves shared household expenses alongside personal accounts.

### Decision
We established the **Household** as the core tenant boundary. All accounts, categories, transactions, budgets, bills, and goals belong to a `Household`. Users belong to one or more households via `household_members`.

### Consequences
- **Advantages**:
  - Allows spouses, partners, and family members to collaborate on a single shared budget.
  - Clear multi-tenant scoping in database queries (`household_id`).
  - Supports flexible roles (`Owner`, `Admin`, `Member`, `Viewer`).
- **Trade-offs**:
  - All queries must be filtered by `household_id` (enforced via EF Core Global Query Filters).

---

## ADR-007: Mixed Financial Visibility (Household, Private, SelectedMembers)

### Context
In real-world families, members want to track shared expenses together, but frequently maintain personal discretionary accounts or surprise purchases that should remain private. Forcing either 100% public or 100% segregated models fails family needs.

### Decision
We introduced three granular visibility levels for financial records:
1. `Household`: Visible to all members and aggregated into shared metrics.
2. `Private`: Visible only to the creating user; excluded from shared dashboards.
3. `SelectedMembers`: Shared selectively with specific designated family members via `access_grants`.

### Consequences
- **Advantages**:
  - Matches authentic family financial dynamics.
  - Preserves personal autonomy while fostering collective goals.
- **Trade-offs**:
  - Requires strict server-side aggregation logic to prevent private balance leakage through aggregate totals or Net Worth calculations.

---

## ADR-008: Invitation-Only Registration

### Context
Finora is designed as a private, secure household application. Open public registration exposes the system to bot signups, unsolicited spam, and unauthorized resource consumption.

### Decision
We enforce **Invitation-Only Registration**.
- The first administrator is provisioned using a secure environment seed token (`INITIAL_SETUP_TOKEN`).
- Subsequent users can only register by presenting a valid, unexpired, cryptographic invitation token issued by an existing household administrator.

### Consequences
- **Advantages**:
  - Closes public signup attack surfaces.
  - Eliminates bot accounts and spam registrations.
  - Ensures every registered user is immediately associated with an authorized household.
- **Trade-offs**:
  - Requires administrators to generate invite links for new family members.

---

## ADR-009: Integer Paise (BIGINT) for INR Monetary Representation

### Context
Floating-point numbers (`float`, `double`) in computing cannot represent decimal fractions precisely due to binary IEEE 754 encoding, leading to rounding errors (e.g. `0.1 + 0.2 = 0.30000000000000004`). In financial accounting, fractional rounding drift is intolerable.

### Decision
All monetary values in Finora are stored and calculated as **integer paise** using `BIGINT` in PostgreSQL and `long` in C#.
- ₹1.00 = `100` paise.
- ₹1,250.50 = `125050` paise.
Conversion to decimal currency format is performed strictly at the presentation boundary.

### Consequences
- **Advantages**:
  - Exact integer arithmetic with zero floating-point rounding errors.
  - High performance in database aggregations (`SUM(amount_paise)`).
  - Consistent across backend, database, and frontend.
- **Trade-offs**:
  - Formatting and parsing logic is required when accepting user input and displaying numbers in the UI.

---

## ADR-010: NG-ZORRO Component Library with Tailwind CSS 4

### Context
We evaluated UI component solutions for Angular 20+. Building custom accessible tables, modals, dropdowns, and date pickers from scratch is costly, bug-prone, and violates Ponytail minimalism.

### Decision
We adopted **NG-ZORRO** (`ng-zorro-antd`) as our primary enterprise UI component library, paired with **Tailwind CSS 4** for layout, spacing, and micro-styling.

### Consequences
- **Advantages**:
  - Complete suite of battle-tested, accessible UI primitives (data tables with sorting/filtering, modal dialogs, date pickers, statistics cards).
  - Rapid UI development following standard design patterns.
  - Tailwind CSS 4 provides modern utility styling without bloating component CSS.
- **Trade-offs**:
  - Must adhere to NG-ZORRO design patterns rather than ad-hoc custom HTML.

---

## ADR-011: Ponytail Radical Minimalism Engineering Methodology

### Context
Software projects frequently suffer from overengineering, premature abstractions (e.g., generic repository wrappers over EF Core, MediatR handlers for simple CRUD), and unnecessary dependencies.

### Decision
We mandated the **Ponytail Methodology** (`FULL` mode) across the entire project lifecycle, governed by the Seven-Rung Ladder.

### Consequences
- **Advantages**:
  - Keeps the codebase lean, readable, and maintainable.
  - Eliminates speculative code, unnecessary packages, and boilerplate layers.
  - Preserves security, data validation, and financial correctness.
- **Trade-offs**:
  - Requires developers to evaluate YAGNI and standard platform capabilities before writing code.

