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
| 404 | The id does not exist |
| 409 | A budget already exists for that month |
| 500 | Unexpected error. The response has a generic message only, with no stack trace |

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

### Health
`GET /api/health` returns `200 { "status": "ok" }`. Milestone 7 uses it to show
that EC2 #1 can reach EC2 #2.
