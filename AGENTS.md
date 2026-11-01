# AGENTS.md: Finora Engineering Guidelines

This document defines the persistent engineering standards, architectural constraints, and operational methodology for the Finora project. All agents and developers working on this codebase must adhere strictly to these instructions.

---

## 1. Ponytail Methodology

Finora enforces the official **Ponytail Methodology** (`FULL` mode) across all design, refactoring, and code implementation tasks.

### The Decision Ladder (Before Writing Code)
Before writing any code or introducing any new file, evaluate:
1. **Does this functionality need to exist?** (YAGNI — reject speculative features and unnecessary options).
2. **Does the code already exist?** (Search and reuse existing project utilities, services, and components).
3. **Can an existing platform feature solve it?** (Use native browser, CSS, runtime standard library, or SQL features).
4. **Can an installed dependency solve it?** (Leverage NG-ZORRO, Tailwind CSS 4, Npgsql, EF Core).
5. **What is the smallest correct implementation?** (Write the minimum verifiable code that satisfies requirements).

### Anti-Patterns to Avoid
- **Unnecessary abstractions**: No wrapper layers that just pass calls through.
- **Unnecessary dependencies**: Never install an npm or NuGet package for something solvable in a few lines of standard library code.
- **Duplicate implementations**: Inspect existing services and helpers before writing new logic.
- **Premature optimization**: Write clear, direct code first; optimize only with profiler evidence.
- **Overengineering**: No speculative plugin architectures or excessive design patterns.
- **Generic repositories**: Do not introduce generic repositories (`IRepository<T>`) or unit-of-work wrappers over EF Core `DbContext`. EF Core is already an abstraction over the database.
- **Speculative features**: Only build what is required for the current milestone.

> **Crucial Rule**: Never compromise financial correctness, authorization security, validation, or testing for brevity.

---

## 2. Angular Conventions

- **Angular Version**: Angular 20+.
- **Components**: 100% standalone components (`imports: [...]`). No `NgModule` modules.
- **UI Library**: **NG-ZORRO** (`ng-zorro-antd`) is the mandatory primary UI component library.
  - *Always prefer NG-ZORRO components* (`nz-button`, `nz-table`, `nz-modal`, `nz-form`, `nz-select`, `nz-date-picker`, etc.) before creating custom UI elements.
  - Do not create custom component libraries or reinvent existing UI primitives.
- **Styling**: **Tailwind CSS 4** for layout, spacing, flexbox/grid, and utility styling. Combine Tailwind utilities cleanly with NG-ZORRO themes.
- **State & Reactivity**: Use **Angular Signals** (`signal`, `computed`, `effect`) for synchronous UI state and component-level reactive properties.
- **Asynchronous Operations**: Use **RxJS** for HTTP streams, event debouncing, and complex asynchronous pipelines.
- **Forms**: Use **Reactive Forms** (`FormGroup`, `FormControl`, `NonNullableFormBuilder`) with explicit client-side validators.
- **Architecture**: Feature-based directory architecture:
  ```text
  frontend/src/app/
  ├── core/          # Singletons, auth interceptor, guards, base API client
  ├── shared/        # Reusable UI directives, pipes, shared dialogs
  └── features/      # Feature slices (auth, accounts, transactions, budgets, analytics)
  ```
- **Routing**: Lazy-loaded routes using `loadComponent` / `loadChildren`.
- **API Communication**: Dedicated API services separated cleanly from components. Components consume services; components never invoke `HttpClient` directly.

---

## 3. .NET Conventions

- **Runtime**: **.NET 10** (C# 13).
- **ORM**: **Entity Framework Core 10** with **Npgsql.EntityFrameworkCore.PostgreSQL**.
- **Authentication**: **ASP.NET Core Identity** (Cookie and Bearer Token support). Never mix with Supabase Auth.
- **Architecture**: **Modular Monolith** with clean architectural boundaries:
  - `Finora.Domain`: Entities, Enums, Value Objects, Domain Exceptions (zero external dependencies).
  - `Finora.Application`: DTOs, Service Interfaces, Business Logic, FluentValidators.
  - `Finora.Infrastructure`: EF Core `DbContext`, Identity configuration, Migrations, External integrations.
  - `Finora.Api`: Controllers, Middleware, Health checks, Program configuration.
- **EF Core Configuration**: Use **Fluent API** configurations (`IEntityTypeConfiguration<T>`) in dedicated configuration classes. Avoid polluting domain entities with data annotations.
- **DTOs**: Always accept and return DTOs in API controllers. Never expose domain entities directly to clients.
- **Validation**: Explicit validation using **FluentValidation** on all incoming command/query DTOs.
- **Asynchronous Operations**: All I/O operations must be `async` and accept a `CancellationToken`.
- **Simplicity**: No unnecessary CQRS or MediatR event sourcing. Use straightforward application services unless complex bounded context isolation genuinely demands it.

---

## 4. PostgreSQL Conventions

- **Provider**: Supabase Managed PostgreSQL.
- **Keys**: UUID primary keys (`gen_random_uuid()`).
- **Naming**: Strict `snake_case` naming for tables, columns, constraints, and indexes.
- **Monetary Storage**:
  - Store all currency amounts (INR) as **`BIGINT` in paise** (1 INR = 100 paise; ₹1,250.50 = `125050`).
  - Never store monetary balances or amounts as `FLOAT` or `DOUBLE`.
- **Investment & Asset Quantities**: Use `NUMERIC(18, 6)` for mutual fund units, stock shares, and crypto fractions.
- **Timestamps**: All timestamps must be `TIMESTAMPTZ` stored in UTC.
- **Financial Dates**: Use `DATE` for transaction dates, bill dates, and budget calendar boundaries.
- **Multi-Tenancy & Isolation**:
  - Every tenant-scoped table must include `household_id UUID NOT NULL REFERENCES households(id) ON DELETE CASCADE`.
  - Use composite foreign keys and composite indexes where appropriate (e.g., `(household_id, account_id)`) to enforce tenancy boundary integrity.
- **Migrations**: All schema changes must be driven by reviewed, reproducible EF Core migrations.

---

## 5. Financial Correctness Rules

- **No Floating-Point Math**: Zero tolerance for `float` or `double` in any financial calculation. All arithmetic is integer-based in paise or uses high-precision `decimal`.
- **Transfers Are Not Expenses**: A transfer between two owned accounts changes account balances but must NOT increase household expenses or income in budget or analytics calculations.
- **Credit Card Repayments**: Paying a credit card bill is a transfer from a bank account to a liability account; it must never create a duplicate expense on top of the original transactions.
- **Loan Principal vs. Interest**: Loan repayments must split principal reduction from interest expenditure. Only the interest portion counts as a finance expense; the principal portion reduces debt liability.
- **Savings Contributions**: Transfers into savings goals or sinking funds are allocations of capital, not expenditures.
- **Private Financial Data Isolation**: Records marked `Private` must be strictly excluded from shared household totals, multi-member dashboard summaries, and exports for non-permitted members.
- **Atomic Mutations**: All multi-step financial operations (transfers, balance reconciliations, split allocations) must execute inside an explicit database transaction (`IDbContextTransaction`).

---

## 6. Security Standards

- **No Client-Side Secrets**: Never expose Supabase service role keys, master passwords, or database credentials in Angular or client bundles.
- **Server-Enforced Tenancy**: Never trust `householdId` from client request bodies or query params. Extract the caller's identity and authorized household membership from the verified server-side session/token.
- **Record Visibility Filtering**: Filter transactions, accounts, and budgets by visibility (`Household`, `Private`, `SelectedMembers`) in server queries before returning data.
- **Input Validation**: Validate every request against strict schemas before processing.
- **Audit Logging**: Maintain an immutable audit log for financial modifications, permission changes, and invitations.
- **No Sensitive Logs**: Never log account numbers, passwords, authorization tokens, or sensitive monetary balances.

---

## 7. Testing Requirements

- Write unit tests for all financial calculations (interest calculations, split validations, balance adjustments, paise conversions).
- Write integration tests for household tenancy authorization and record visibility filtering.
- **Never claim a test passed unless it was actually executed and verified via test runners.**
- Always run regression tests after modifying existing code.

---

## 8. Completion Reporting Format

At the completion of every implementation task, provide a concise report covering:
1. **Files Changed**: Exact paths created or updated.
2. **Features Implemented**: Clear summary of functionality added.
3. **Tests Executed**: Commands run and verified test results.
4. **Security Considerations**: Tenancy, authorization, and validation rules verified.
5. **Deferred Work**: Items postponed to future milestones.
6. **Technical Debt**: Any intentional shortcuts tagged with `// ponytail:` comments.

