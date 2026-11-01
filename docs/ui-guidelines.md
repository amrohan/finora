# Finora UI & Frontend Design Guidelines

This guide establishes the visual language, component usage, and responsive styling conventions for Finora's Angular 20+ frontend.

---

## 1. Design Principles

1. **Clarity Over Clutter**: Financial clarity comes from clean spacing and clear visual hierarchy. Avoid dense, unformatted tables and cramped data cards.
2. **Predictable Component Standards**: Prefer official **NG-ZORRO** components over custom-built components. Avoid custom component sprawl.
3. **Responsive-First**: The application must function flawlessly as a mobile PWA on smartphones (375px+) and expand gracefully to ultrawide desktop monitors (1920px+).
4. **Financial Legibility**: Monospaced font figures (`font-mono`) for numerical values to ensure decimal alignment in tables and financial reports.
5. **Privacy Consciousness**: Include a persistent, global Privacy Toggle that masks balances (`••••••`) when in public view.

---

## 2. Component Standards: NG-ZORRO

| UI Pattern | NG-ZORRO Component | Implementation Standard |
| :--- | :--- | :--- |
| **Buttons** | `nz-button` | Use `nzType="primary"` for primary action; `nzDanger` for destructive actions. |
| **Tables** | `nz-table` | Server-side pagination, built-in sorting, empty state templates (`nz-empty`). |
| **Forms** | `nz-form`, `nz-form-item`, `nz-form-control` | Clear labels, integrated validation error tooltips/feedback. |
| **Modals** | `nz-modal` / `NzModalService` | Used for focused workflows (Add Transaction, Record Transfer). |
| **Drawers** | `nz-drawer` | Used for detailed inspection (Transaction details, audit history). |
| **Selects** | `nz-select` | Searchable, grouped categories with category icons. |
| **Date Pickers**| `nz-date-picker`, `nz-range-picker` | Native keyboard accessible, formatted as `YYYY-MM-DD`. |
| **Stats Cards** | `nz-card`, `nz-statistic` | Net worth cards, monthly cash flow cards with delta indicators. |
| **Progress** | `nz-progress` | Budget consumption bars with color thresholds (Green < 75%, Amber 75-99%, Red >= 100%). |
| **Alerts** | `nz-alert`, `nz-notification` | Persistent warnings for overdue bills; toast notifications for mutation outcomes. |

---

## 3. Styling Standards: Tailwind CSS 4

Tailwind CSS 4 is used for structure, flex/grid layouts, responsive breakpoints, and spacing utilities:

- **Grid Systems**:
  ```html
  <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
    <!-- Stat Cards -->
  </div>
  ```
- **Typography**:
  - Headings: `text-2xl font-semibold tracking-tight text-slate-900 dark:text-slate-100`
  - Subheadings: `text-sm font-medium text-slate-500 dark:text-slate-400`
  - Financial Figures: `font-mono font-medium tracking-tight`
- **Color Palette**:
  - Primary Brand: Emerald / Teal (`emerald-600` / `emerald-500` representing financial health).
  - Income / Inflow: Green (`emerald-600` / `emerald-400`).
  - Expense / Outflow: Rose (`rose-600` / `rose-400`).
  - Transfers: Blue (`sky-600` / `sky-400`).
  - Surface Backgrounds: `bg-slate-50 dark:bg-slate-950`.
  - Card Backgrounds: `bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800`.

---

## 4. Forms & Validation UX

- **Reactive Forms**: All forms must be authored using Angular's `NonNullableFormBuilder`.
- **Validation Timing**:
  - Validation errors must display only after the user has interacted with the input (`dirty` or `touched`).
  - Submit buttons must indicate loading spinners (`[nzLoading]="isSubmitting()"`) during asynchronous operations.
- **Monetary Input UX**:
  - Monetary inputs accept standard Rupee decimals (e.g. `1250.50`).
  - The form component automatically converts the decimal input into integer paise (`125050`) when submitting the DTO to the backend.

---

## 5. Tables & Data Display

- **Server-Side Operations**: Tables with potentially large datasets (transactions, audit logs) must implement server-side pagination, sorting, and filtering.
- **Empty States**: Every table must render an informative empty state (`nz-empty`) with a clear call-to-action (e.g., "No transactions found for this month. Add your first transaction").
- **Column Alignment**:
  - Text columns (Payee, Category, Notes) are left-aligned.
  - Date and Status columns are center-aligned.
  - Monetary amounts are right-aligned with `font-mono` styling.

---

## 6. Dialogs & Modals

- Simple confirms (e.g., "Delete transaction?") must use `NzModalService.confirm()`.
- Complex data creation (Split Transaction Editor, Transfer Dialog) must use modal templates or standalone modal components with explicit header and footer action bars.
- Modals must be dismissible with the `Escape` key and click-outside (except during active network submission).

---

## 7. Loading, Skeleton & Error States

- **Loading Spinners**: For localized actions (button submits, small fetches), use `nz-spin` or button loading states.
- **Skeleton Screens**: For full-page initial loads (Dashboard, Account list), use `nz-skeleton` cards rather than full-screen blank spinners.
- **Error States**: Display meaningful error boundaries with retry buttons when network requests fail.

---

## 8. Accessibility (WCAG 2.1 AA)

- All interactive controls must be keyboard navigatable (`Tab`, `Enter`, `Space`, `Escape`).
- Maintain minimum contrast ratio of 4.5:1 for standard text and 3:1 for large text.
- Use explicit `aria-label` attributes on icon-only buttons (e.g., quick-add, edit, delete).
- Financial charts must include text-equivalent tables or tooltips for screen readers.

---

## 9. Financial Dashboard Patterns

```text
+-------------------------------------------------------------------------------+
| Header: [Active Household Switcher]            [Privacy Toggle] [User Menu]   |
+-------------------------------------------------------------------------------+
| Stat Cards:                                                                   |
| [ Net Worth: ₹ 14,80,250 ]   [ Income: ₹ 1,20,000 ]   [ Expense: ₹ 65,400 ]   |
+-------------------------------------------------------------------------------+
| Main Body (2 Columns):                                                        |
| +------------------------------------+  +-----------------------------------+ |
| | Monthly Budget Progress            |  | Upcoming Bills (Next 14 Days)     | |
| | Groceries  [=========>   ] 75%     |  | Electricity: ₹ 3,450 (In 3 days)  | |
| | Utilities  [============>] 105%!   |  | Internet:    ₹ 1,199 (In 8 days)  | |
| +------------------------------------+  +-----------------------------------+ |
| +---------------------------------------------------------------------------+ |
| | Recent Transactions Table                                                 | |
| | Date       | Payee            | Category    | Account      | Amount       | |
| | 2026-11-01 | Reliance Fresh   | Groceries   | HDFC Bank    | -₹ 2,450.00  | |
| +---------------------------------------------------------------------------+ |
+-------------------------------------------------------------------------------+
```

