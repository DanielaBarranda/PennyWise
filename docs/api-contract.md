# PennyWise API Contract

The React frontend (Milestone 2) is written against this contract. The
ASP.NET Core API (Milestone 3) must implement it exactly.

All routes start with `/api`. Request and response bodies are JSON with
camelCase property names.

## Conventions

| Item | Format |
|---|---|
| Month | `"YYYY-MM"` string, e.g. `"2026-10"` |
| Date | `"YYYY-MM-DD"` string, no time or time zone |
| Money | JSON number, stored as `decimal` in C#. Must be > 0, at most 2 decimal places, at most 10,000,000 |
| Category | One of `Food`, `Transportation`, `School`, `Bills`, `Entertainment`, `Others` (sent as a string) |
| Warning threshold | 80% of a budget or category limit |

### Errors
Errors use ASP.NET Core's standard **ProblemDetails** format:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "amount": ["Amount must be greater than 0."] }
}
```

| Status | When |
|---|---|
| 400 | Validation failed (`errors` lists each field) |
| 401 | Not signed in, or the session has ended |
| 403 | A data-changing request without the `X-PennyWise-Client` header |
| 404 | The id does not exist, **or belongs to another user** (the two look identical on purpose) |
| 409 | A budget already exists for that month, or the email is already registered |
| 429 | Too many sign-in attempts (per IP per minute), or the account is locked after 5 wrong passwords |
| 500 | Unexpected error. The response has a generic message only, with no stack trace |

## Accounts and sign-in

Every endpoint below requires a signed-in user, except `POST /api/auth/register`,
`POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me` and `GET /api/health`.

- **Session:** signing in sets the cookie `pennywise_session` (HttpOnly, SameSite=Strict,
  Secure in production, 7 days, renewed while in use). The browser sends it automatically;
  JavaScript cannot read it.
- **Ownership:** budgets, expenses, savings goals and bills belong to the signed-in user. The user is taken from
  the cookie on the server, never from the request; a `userId` sent in a body is ignored.
- **CSRF protection:** `POST`, `PUT` and `DELETE` requests must include the header
  `X-PennyWise-Client: web` (the web app always sends it).

| Method | Route | Body | Success |
|---|---|---|---|
| POST | `/api/auth/register` | `{ name, email, password, confirmPassword }` | 201 `User`, signed in |
| POST | `/api/auth/login` | `{ email, password }` | 200 `User`, signed in |
| POST | `/api/auth/logout` | | 204, cookie removed |
| GET | `/api/auth/me` | | 200 `User`, or 204 when nobody is signed in |
| PUT | `/api/auth/profile` | `{ name }` | 200 `User` (the email cannot be changed) |
| PUT | `/api/auth/preferences` | `{ allowancePeriod }` (`Daily`, `Weekly` or `Monthly`) | 200 User |
| POST | `/api/auth/change-password` | `{ currentPassword, newPassword, confirmPassword }` | 204; this browser stays signed in, every other device is signed out |

User is `{ id, name, email, memberSince, allowancePeriod }`.

Rules: name 1–80 characters; a valid email up to 254 characters, unique ignoring case and
spaces; password 8–128 characters with at least one letter and one number; a wrong email
and a wrong password both return the same 401 message. Password hashes are never returned.

## Objects

**Expense**
```json
{ "id": 12, "amount": 85.50, "category": "Food", "description": "Lunch at canteen",
  "expenseDate": "2026-10-05", "createdAt": "2026-10-05T04:12:00Z", "updatedAt": "2026-10-05T04:12:00Z" }
```

**Budget** (one per month)
```json
{ "id": 3, "month": "2026-10", "amount": 8000.00,
  "categoryLimits": [ { "category": "Food", "amount": 3000.00 } ] }
```

## Endpoints

### Dashboard
`GET /api/dashboard/summary?month=YYYY-MM` (defaults to the current month)

```json
{
  "month": "2026-10",
  "budget": { "...Budget object..." },
  "totalSpent": 6720.50,
  "remaining": 1279.50,
  "percentSpent": 84.01,
  "status": "Warning",
  "expenseCount": 23,
  "categories": [
    { "category": "Food", "total": 3150.00, "limit": 3000.00, "status": "Exceeded" },
    { "category": "School", "total": 0, "limit": null, "status": "NoLimit" }
  ],
  "recentExpenses": [ "...up to 5 Expense objects, newest expenseDate first..." ]
}
```

- `budget` is `null` when the month has no budget. In that case `remaining` and `percentSpent` are `null`, and `status` is `"NoBudget"`.
- `status` is `"OnTrack"` below 80%, `"Warning"` from 80% up to 100%, and `"Exceeded"` above 100%.
- `categories` always has all six categories. Each one's `status` is `"NoLimit"`, `"OnTrack"`, `"Warning"` or `"Exceeded"`.

### Budgets
| Method | Route | Body | Success |
|---|---|---|---|
| GET | `/api/budgets?month=YYYY-MM` | | 200, array of Budget (`month` is optional) |
| GET | `/api/budgets/{id}` | | 200 Budget, or 404 |
| POST | `/api/budgets` | `{ month, amount, categoryLimits[] }` | 201 Budget, or 409 if the month already has one |
| PUT | `/api/budgets/{id}` | `{ month, amount, categoryLimits[] }` | 200 Budget |

Validation: each category can appear at most once in `categoryLimits`, and all the limits together cannot add up to more than `amount`.

### Expenses
| Method | Route | Body | Success |
|---|---|---|---|
| GET | `/api/expenses?month=YYYY-MM&category=Food` | | 200, array of Expense, newest first (both filters are optional) |
| GET | `/api/expenses/{id}` | | 200 Expense, or 404 |
| POST | `/api/expenses` | `{ amount, category, description, expenseDate }` | 201 Expense |
| PUT | `/api/expenses/{id}` | `{ amount, category, description, expenseDate }` | 200 Expense |
| DELETE | `/api/expenses/{id}` | | 204 |

Validation: `description` is required, is trimmed, and is at most 200 characters.

### Savings goals
A goal's saved amount is always worked out from its deposits and withdrawals; it is never
stored or typed in. Saving does not count as spending.

| Method | Route | Body | Success |
|---|---|---|---|
| GET | `/api/savings?includeArchived=false` | | 200, array of Goal |
| GET | `/api/savings/{id}` | | 200 Goal, or 404 |
| POST | `/api/savings` | `{ name, description?, targetAmount, targetDate? }` | 201 Goal |
| PUT | `/api/savings/{id}` | same as POST | 200 Goal |
| POST | `/api/savings/{id}/archive` · `/restore` | | 200 Goal |
| GET | `/api/savings/{id}/transactions` | | 200, array, newest first |
| POST | `/api/savings/{id}/transactions` | `{ type: "Contribution" \| "Withdrawal", amount, date, note? }` | 201 |

Goal: `{ id, name, description, targetAmount, savedAmount, remainingAmount, percentSaved,
targetDate, daysLeft, expectedByToday, status: "Active" | "Completed" | "Archived", createdAt }`.
Rules: a new target date cannot be in the past; transactions cannot be dated in the future;
a withdrawal cannot exceed the saved amount; archived goals accept no transactions.

### Bills
A bill is a schedule (`OneTime`, `Weekly`, `Monthly` or `Yearly`), so its due dates are
generated, never duplicated. A monthly bill due on the 31st falls on the last day of
shorter months. Due dates before the bill was added are not treated as owed.

| Method | Route | Body | Success |
|---|---|---|---|
| GET | `/api/bills?includeArchived=false` | | 200, array of Bill |
| GET | `/api/bills/{id}` | | 200 Bill, or 404 |
| GET | `/api/bills/occurrences?from=YYYY-MM-DD&to=YYYY-MM-DD` | | 200, due dates in the range (default: 1st of this month to 60 days ahead) |
| POST | `/api/bills` | `{ name, amount, category, frequency, firstDueDate, endDate?, notes? }` | 201 Bill |
| PUT | `/api/bills/{id}` | same as POST | 200 Bill |
| POST | `/api/bills/{id}/archive` · `/restore` | | 200 Bill |
| POST | `/api/bills/{id}/payments` | `{ dueDate, paidOn?, recordExpense = true }` | 200; 409 if that due date is already paid |
| DELETE | `/api/bills/{id}/payments/{dueDate}` | | 204; also deletes the linked expense |

Occurrence: `{ billId, name, amount, category, frequency, dueDate, status: "Paid" | "Overdue" |
"DueSoon" | "Upcoming", daysUntilDue, paidOn, paidAmount, expenseId }`. "Due soon" means within
7 days. Paying with `recordExpense` creates exactly one linked expense. Reminders are shown in
the app only; no email or push notifications are sent.

### Allowance
`GET /api/allowance?month=YYYY-MM&period=Daily|Weekly|Monthly` (period defaults to the saved preference)

```json
{ "month": "2026-10", "period": "Daily", "status": "Available", "budget": 10000, "spent": 1749,
  "reservedForBills": 599, "available": 7652, "daysLeft": 3, "daysLeftInWeek": 3,
  "amount": 2550.66, "daily": 2550.66, "weekly": 7651.98, "monthly": 7652 }
```

`available = budget − spent − unpaid bills due this month` (overdue ones included). Daily is
what is left divided by the days left (today included, Manila time), rounded down to the
centavo; weekly is daily × days left until Sunday; monthly is everything left. `status` is
`Available`, `OverBudget`, `MonthEnded` or `NoBudget`.

### Insights
`GET /api/insights?month=YYYY-MM` returns rule-based notes, most urgent first:
`{ key, severity: "Critical" | "Warning" | "Success" | "Info", kind: "Fact" | "Suggestion",
title, message, link, dismissible }`. Critical ones (overdue bills, budget exceeded) cannot be
dismissed. Dismissals are remembered in the browser, per account.

### Monthly report
- `GET /api/reports/monthly?month=YYYY-MM`: budget, total spent, comparison with the
  previous month, categories, savings, bills, insights and notes.
- `GET /api/reports/monthly.csv?month=YYYY-MM`: the same report as a CSV download (UTF-8).

Paid bills recorded as expenses are counted once, in spending. Income is not recorded in
PennyWise, so the report shows no income or net cash flow.

### Health
`GET /api/health` returns `200 { "status": "ok" }`. Milestone 7 uses it to show
that EC2 #1 can reach EC2 #2.
