# Finora REST API Conventions

This document specifies the standard conventions for all HTTP REST API endpoints in the Finora backend.

---

## 1. Versioning & Route Structure

- **API Base URI**: `/api/v1`
- **Resource Naming**: Plural nouns in `kebab-case` or lowercase (e.g., `/api/v1/accounts`, `/api/v1/transactions`, `/api/v1/recurring-bills`).
- **HTTP Methods**:
  - `GET`: Safe, idempotent read operations.
  - `POST`: Create a new resource or execute a non-idempotent action.
  - `PUT`: Complete update of an existing resource.
  - `PATCH`: Partial update of an existing resource.
  - `DELETE`: Remove or soft-archive a resource.

### Route Patterns
```text
GET    /api/v1/households                      # List households for current user
GET    /api/v1/households/{id}                 # Get household details
GET    /api/v1/accounts                        # List accounts in current household
POST   /api/v1/accounts                        # Create new account
GET    /api/v1/accounts/{id}                   # Get account details
PUT    /api/v1/accounts/{id}                   # Update account
DELETE /api/v1/accounts/{id}                   # Archive account

GET    /api/v1/transactions                    # Search/list transactions (paged)
POST   /api/v1/transactions                    # Record transaction (expense/income)
POST   /api/v1/transactions/transfers          # Record account transfer
GET    /api/v1/transactions/{id}               # Get transaction details
PUT    /api/v1/transactions/{id}               # Update transaction
DELETE /api/v1/transactions/{id}               # Delete transaction

GET    /api/v1/budgets/monthly                 # Get budget breakdown for month/year
PUT    /api/v1/budgets                         # Upsert category budget allocation

GET    /api/v1/analytics/net-worth             # Get net worth trend
GET    /api/v1/analytics/cash-flow             # Get income vs expense cash flow
```

---

## 2. DTO (Data Transfer Object) Conventions

- **PascalCase in C# / camelCase in JSON**: C# DTO properties are `PascalCase`, serialized to `camelCase` over the wire by `System.Text.Json`.
- **Immutability**: DTOs are declared as C# `record` or `readonly record struct` types.
- **Monetary Units**: All monetary fields in request/response DTOs are integer paise (`amountPaise: long`). Never pass raw floating-point floats.
- **Dates**:
  - Timestamps are formatted as ISO 8601 strings in UTC (`2026-11-01T06:00:00Z`).
  - Financial calendar dates are formatted as `YYYY-MM-DD` (`2026-11-01`).

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "accountId": "7ca64b22-8349-4702-8611-9a9108b9812b",
  "categoryId": "12e96a41-2a1e-4509-9134-453d86f7b49e",
  "transactionType": "Expense",
  "amountPaise": 150000,
  "date": "2026-11-01",
  "payee": "Supermarket Reliance Fresh",
  "visibility": "Household",
  "isSplit": false,
  "createdAt": "2026-11-01T05:59:12Z"
}
```

---

## 3. Standard Response Wrappers & Pagination

### Single Resource Response
Successful operations return HTTP 200 (OK) with the resource body, or HTTP 201 (Created) with a `Location` header.

### Paginated List Response (`PagedResult<T>`)
List endpoints accepting pagination query parameters (`page` [1-based], `pageSize` [default 20, max 100]) return a standard structure:

```json
{
  "items": [ ... ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 142,
  "totalPages": 8,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

Query parameters:
- `page` (int, default: 1)
- `pageSize` (int, default: 20)
- `sortBy` (string, e.g. "date")
- `sortOrder` ("asc" | "desc", default: "desc")
- `startDate` (YYYY-MM-DD)
- `endDate` (YYYY-MM-DD)
- `accountId` (UUID)
- `categoryId` (UUID)

---

## 4. Error Responses (RFC 7807 Problem Details)

All error responses adhere to the standard **RFC 7807 Problem Details** format with appropriate HTTP status codes:

- `400 Bad Request`: Schema or validation failure.
- `401 Unauthorized`: Missing or invalid authentication token.
- `403 Forbidden`: Authenticated user lacks access to the household or record.
- `404 Not Found`: Resource does not exist or user lacks visibility.
- `409 Conflict`: Business rule violation (e.g. duplicate category name, concurrency conflict).
- `422 Unprocessable Entity`: Financial domain invariant failure (e.g., split amounts do not balance).
- `500 Internal Server Error`: Unhandled server exception.

### Validation Error Example (400)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00",
  "errors": {
    "AmountPaise": ["The amount must be greater than zero."],
    "Date": ["Transaction date cannot be in the future."]
  }
}
```

---

## 5. Input Validation

- Handled using **FluentValidation** on the backend.
- Validators are executed automatically by ASP.NET Core filters before action method execution.
- Rules validate:
  - Range and positivity of monetary amounts.
  - Required foreign keys (`AccountId`, `CategoryId`).
  - String length bounds to prevent DoS via payload size.
  - Valid enum values for types and visibilities.

---

## 6. Idempotency Support

For critical financial mutations (creating transactions, transfers, loan payments), the API supports the **`Idempotency-Key`** HTTP header:

```http
POST /api/v1/transactions/transfers
Idempotency-Key: 7b2d4f55-199b-449e-b9b5-25b849e7cf81
Content-Type: application/json
```

- When present, the server caches the result of the request for 24 hours.
- If a client retries a network request with the same `Idempotency-Key`, the server returns the cached response rather than creating a duplicate financial transfer.

---

## 7. Authentication & Authorization

### Authentication
- Authentication uses ASP.NET Core Identity with **HTTP-Only Secure Cookies** for web browser sessions, and optional **Bearer JWT tokens** for mobile/PWA background sync:
  ```http
  Authorization: Bearer <jwt-token>
  ```
- Refresh tokens are rotated on each issuance; revoking a session invalidates all active refresh tokens.

### Household Tenancy Context Header
- The active household context is transmitted via header:
  ```http
  X-Household-Id: 9b2d3c11-7443-41bb-b89e-21ef68d37443
  ```
- **Security Check**: The server's `TenancyMiddleware` extracts the authenticated user's ID, verifies from the database that the user is an active member of `X-Household-Id`, and sets the tenant context. If the user is not a member of the requested household, the request is immediately rejected with `403 Forbidden`.

### Authorization Policies
- `RequireHouseholdMember`: Requires caller to belong to the requested household.
- `RequireHouseholdAdmin`: Requires role `Owner` or `Admin`.
- `RequireRecordVisibility`: Verifies that private records are accessible only to the owner or designated grant recipients.

