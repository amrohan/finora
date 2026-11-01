# Finora 🪙

> **Modern, privacy-first personal and family wealth management.**

Finora is a family finance management web application and PWA built for multi-member households. It balances collective transparency (shared budgets, recurring bills, emergency funds) with personal autonomy (private accounts, discretionary spending, selective visibility).

---

## 🌟 Key Features

- **Multi-Member Family Tenancy**: Shared household finances with granular role-based permissions (`Owner`, `Admin`, `Member`, `Viewer`).
- **Privacy-Aware Financial Visibility**:
  - `Household`: Fully shared across family members.
  - `Private`: Visible only to the owner; excluded from shared aggregations.
  - `SelectedMembers`: Selectively shared with designated family members.
- **Transaction-Based Accounting**: Immutable double-entry inspired ledger. Transfers between accounts adjust balances without polluting income or expense metrics.
- **Indian Financial Localization**:
  - Currency: Indian Rupee (INR ₹), stored internally as integer paise (`BIGINT`) with zero floating-point rounding drift.
  - Timezone: `Asia/Kolkata`.
  - Number formatting: Indian numbering system (`₹ 1,00,000.00`).
- **Planning & Commitments**: Monthly category budgets, rollover support, recurring bill calendars, and payment tracking.
- **Wealth & Debt Tracking**: Loan amortization schedules, principal-vs-interest split tracking, and savings goals with progress milestones.
- **Progressive Web App (PWA)**: Installable on iOS, Android, macOS, and Windows with offline support.
- **Privacy Shield**: One-click UI toggle to mask monetary balances when viewing finances in public spaces.

---

## 🛠️ Technology Stack

| Layer | Technology |
| :--- | :--- |
| **Frontend Framework** | Angular 20+ (Standalone Components, Signals, RxJS) |
| **UI Components** | NG-ZORRO (`ng-zorro-antd`) |
| **CSS & Layout** | Tailwind CSS 4 |
| **Backend API** | .NET 10 (C# 13) Web API |
| **ORM & Data Access** | Entity Framework Core 10 (`Npgsql`) |
| **Database** | Supabase Managed PostgreSQL 16+ |
| **Authentication** | ASP.NET Core Identity (Cookie & Bearer Token) |
| **Reverse Proxy** | Caddy v2 (Automatic Let's Encrypt HTTPS, HTTP/3) |
| **Methodology** | Ponytail Radical Minimalism (`FULL` mode) |

---

## 📁 Repository Structure

```text
finora/
├── GEMINI.md                     # Agent operational instructions
├── AGENTS.md                     # Persistent engineering guidelines
├── README.md                     # Project documentation entry point
├── .editorconfig                 # Multi-language formatting standards
├── .gitignore                    # Version control ignore definitions
├── .env.example                  # Environment variable configuration template
│
├── docs/                         # Comprehensive architectural documentation
│   ├── architecture.md           # Modular monolith, tenancy, and boundaries
│   ├── database-schema.md        # Relational schema, ER diagram, paise conventions
│   ├── implementation-plan.md    # 6-phase engineering plan with criteria
│   ├── decisions.md              # Architectural Decision Records (ADRs)
│   ├── api-conventions.md        # REST API endpoints, DTOs, and error standards
│   ├── security.md               # Auth, tenancy isolation, encryption, threat model
│   ├── ui-guidelines.md          # NG-ZORRO components, Tailwind 4, UX standards
│   └── deployment.md             # Supabase, Caddy, VPS Docker, and backups
│
├── frontend/                     # Angular 20+ SPA and PWA source code
├── backend/                      # .NET 10 Web API and domain modules
│
├── infrastructure/               # Deployment and operations assets
│   ├── docker/                   # Dockerfiles and compose manifests
│   ├── caddy/                    # Caddyfile reverse proxy configuration
│   ├── scripts/                  # Automated backup, restore, and deploy scripts
│   └── supabase/                 # Database initialization and seed scripts
│
├── .github/                      # CI/CD automation
│   └── workflows/                # GitHub Actions test, build, and deploy
│
└── .agents/                      # Ponytail methodology rules & skills
    ├── rules/ponytail.md         # Always-on minimalism rules
    └── skills/ponytail/SKILL.md  # On-demand decision ladder skill
```

---

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+ & npm](https://nodejs.org/)
- [Angular CLI 20+](https://angular.dev/)
- [Docker & Docker Compose](https://www.docker.com/) (optional for local DB)
- Supabase account with a PostgreSQL project (or local PostgreSQL 16+)

### 1. Environment Configuration

Copy the template environment file:

```bash
cp .env.example .env
```

Edit `.env` to configure your Supabase PostgreSQL connection string and security keys:

```dotenv
DATABASE_CONNECTION_STRING="Host=aws-0-ap-south-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.<ref>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true;"
JWT_SECRET="your_minimum_32_character_super_secret_key_here"
INITIAL_SETUP_TOKEN="a_secure_one_time_token_for_initial_admin_signup"
```

### 2. Backend Setup (.NET 10)

```bash
cd backend

# Restore dependencies
dotnet restore

# Apply database migrations to Supabase PostgreSQL
dotnet ef database update --project src/Finora.Infrastructure --startup-project src/Finora.Api

# Run the API
dotnet run --project src/Finora.Api
```

The API will be available at `http://localhost:5000` (Swagger UI at `http://localhost:5000/swagger`).

### 3. Frontend Setup (Angular 20+)

```bash
cd frontend

# Install dependencies
npm install

# Start local development server
npm start
```

Open `http://localhost:4200` in your browser.

---

## 🧪 Testing

Finora enforces strict verification for financial calculations and tenancy security:

```bash
# Run backend tests
cd backend
dotnet test --logger "console;verbosity=normal"

# Run frontend unit tests
cd frontend
npm test
```

> **Engineering Rule**: Never claim a test passed unless it was actually executed and verified.

---

## 🔒 Security & Privacy Architecture

- **No Client-Side Supabase Keys**: Angular never communicates directly with Supabase. All traffic routes through the Finora .NET API.
- **Server-Side Tenancy**: Every financial query is filtered by the user's verified household membership.
- **Zero Floating-Point Drift**: All currency values are calculated in integer paise.
- **Invitation-Only**: Public user registration is closed; new users must present a verified invitation token.

---

## 📜 Engineering Methodology

Finora strictly follows the **Ponytail Methodology** (`FULL` mode). Before writing any code:
1. Question whether the feature needs to exist (YAGNI).
2. Reuse existing project patterns and standard library features.
3. Keep diffs focused, minimal, and verifiable.
4. Never compromise validation, financial accuracy, or security.

See [AGENTS.md](file:///Users/rohan/Developer/finora/AGENTS.md) and [docs/decisions.md](file:///Users/rohan/Developer/finora/docs/decisions.md) for complete details.

