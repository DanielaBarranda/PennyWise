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
`POST /api/auth/login`, `POST /api/auth/login/two-factor`, `GET /api/auth/options`,
`POST /api/auth/google`, `POST /api/auth/google/link`, `POST /api/auth/logout`,
`GET /api/auth/me` and `GET /api/health`.

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
| POST | `/api/auth/login` | `{ email, password }` | 200 `User`, signed in; or 200 `SignInStep` (see below) |
| POST | `/api/auth/login/two-factor` | `{ ticket, code }` (6-digit app code or a recovery code) | 200 `User`, signed in; 400 wrong code; 401 ticket expired |
| GET | `/api/auth/options` | | 200 `{ googleClientId }` (`null` when Google sign-in is not set up) |
| POST | `/api/auth/google` | `{ credential }` (the ID token from Google's button) | 200 `User`, or 200 `SignInStep`; 401 if the token is not valid; 404 when Google sign-in is off |
| POST | `/api/auth/google/link` | `{ ticket, password }` | 200 `User` (Google is now connected), or 200 `SignInStep` for the code |
| POST | `/api/auth/logout` | | 204, cookie removed |
| GET | `/api/auth/me` | | 200 `User`, or 204 when nobody is signed in |
| PUT | `/api/auth/profile` | `{ name }` | 200 `User` (the email cannot be changed) |
| PUT | `/api/auth/tour` | `{ completed }` (`true` when the app tour is finished or skipped, `false` to show it again) | 200 User |
| PUT | `/api/auth/preferences` | `{ budgetPeriod }` (`Daily`, `Weekly` or `Monthly`: which budgets the dashboard shows) | 200 User |
| GET | `/api/auth/avatar?v={avatarVersion}` | | 200 the signed-in user's photo (`image/jpeg`, `image/png` or `image/webp`), or 404 |
| PUT | `/api/auth/avatar` | the image bytes (`Content-Type: image/…`) | 200 User |
| DELETE | `/api/auth/avatar` | | 200 User |
| POST | `/api/auth/change-password` | `{ currentPassword, newPassword, confirmPassword }` | 204; this browser stays signed in, every other device is signed out |
| POST | `/api/auth/delete-account` | `{ password }`, or `{ googleCredential }` for an account without a password | 204, signed out. Deletes the account and all of its data; 400 if the password is wrong |
| POST | `/api/auth/two-factor/setup` | | 200 `{ key, uri }`: the authenticator secret (Base32) and its `otpauth://` link for the QR code. Not active yet |
| POST | `/api/auth/two-factor/enable` | `{ code }` | 200 `{ recoveryCodes }` (10, shown once); every other device is signed out |
| POST | `/api/auth/two-factor/disable` | `{ code }` (app code or recovery code) | 200 `User` |
| POST | `/api/auth/two-factor/recovery-codes` | `{ code }` | 200 `{ recoveryCodes }`; the old ones stop working |

User is `{ id, name, email, memberSince, budgetPeriod, avatarVersion, hasPassword, googleConnected,
twoFactorEnabled, recoveryCodesLeft, tourCompleted }`. The full app tour (every page, in turn) opens by
itself while `tourCompleted` is false; "Tour this page" in the menu tours only the current page,
and "Take the full tour" on Profile replays everything. `tourCompleted` is saved on the account, so it never comes back on
another device once finished or skipped. `avatarVersion` is `null` when there is no profile photo
and changes whenever the photo does.

**Sign-in steps.** When signing in needs one more step, the API answers with
`SignInStep` `{ next, ticket, email }` instead of the user, and sets no cookie:
- `next: "twoFactor"`: two-step verification is on. Send the ticket and a code to
  `/api/auth/login/two-factor`. This applies to Google sign-in too.
- `next: "linkGoogle"`: the Google account's email already has a PennyWise password. Send the
  ticket and that password to `/api/auth/google/link` once; after that Google signs straight in.
  (Without this, anyone who made a PennyWise account with someone else's email first could
  take over their data later.)

Tickets are encrypted, last 5 minutes and stop working if the password changes. Wrong passwords
and wrong codes count towards the same lockout (5 tries, then 15 minutes); a right password does
not reset the count while a code is still to come.

**Two-step verification** uses standard time-based codes (RFC 6238: 6 digits, 30 seconds, SHA-1),
so any authenticator app works. The secret is encrypted with ASP.NET Data Protection; recovery
codes are stored only as SHA-256 hashes. Each app code and each recovery code works once.

**Google sign-in** checks the ID token's RS256 signature against Google's published keys, the
issuer, the audience (PennyWise's client ID, setting `PennyWise:GoogleClientId`), the expiry and
`email_verified`. The account is matched by Google's permanent ID (`sub`), not by email. EC2 #2
has no internet access, so it downloads Google's keys through Nginx on EC2 #1
(`PennyWise:GoogleCertsUrl`, `/internal/google-certs`, allowed only from EC2 #2).

Deleting an account removes the user and, through cascading foreign keys, every budget,
category limit, expense, savings goal and transaction, bill and payment, and the photo. Other
signed-in devices are signed out on their next request. Copies taken by the deploy script's
database backups (the 5 most recent deploys) are not changed and expire as new backups replace them.

Profile photos: the web app crops and shrinks them to 256 × 256 JPEG first. The API accepts at
most 256 KB, checks the real file type from its first bytes (JPEG, PNG or WebP only; SVG and
anything else is refused), and serves it with `X-Content-Type-Options: nosniff` and a
`sandbox` Content-Security-Policy. Photos are stored in the database (table `UserAvatars`).

Rules: name 1–80 characters; a valid email up to 254 characters, unique ignoring case and
spaces; password 8–128 characters with at least one letter and one number; a wrong email
and a wrong password both return the same 401 message. Password hashes are never returned.

## Objects

**Expense**
```json
{ "id": 12, "amount": 85.50, "category": "Food", "description": "Lunch at canteen",
  "expenseDate": "2026-10-05", "createdAt": "2026-10-05T04:12:00Z", "updatedAt": "2026-10-05T04:12:00Z" }
```

**Budget** (one per day, week or month)
```json
{ "id": 3, "period": "Weekly", "start": "2026-11-02", "end": "2026-11-08", "month": "2026-11",
  "amount": 1000.00, "categoryLimits": [ { "category": "Food", "amount": 400.00 } ] }
```
A budget covers exactly one period: a day, a week (Monday to Sunday) or a calendar month.
Nothing is converted between periods: a ₱1,000 weekly budget is ₱1,000 for that week and never
turns into a monthly figure. Each period has at most one budget; periods may overlap (a week can
have a budget while its month has one too, or none).

**Choosing a period in a query.** The dashboard, allowance and insights endpoints (and the budget
list) take either `?month=YYYY-MM` (that month) or `?period=Daily|Weekly|Monthly&date=YYYY-MM-DD`
(the day, week or month containing that date). Without either, they use the current month.

## Endpoints

### Dashboard
`GET /api/dashboard/summary?month=YYYY-MM` or `?period=Weekly&date=YYYY-MM-DD` (defaults to the current month).
Everything in it is for that one period: its own budget and the expenses dated inside it.

```json
{
  "period": "Monthly", "start": "2026-10-01", "end": "2026-10-31",
  "month": "2026-10",
  "budget": { "...Budget object..." },
  "totalSpent": 6520.50,
  "saved": 200.00,
  "remaining": 1279.50,
  "percentSpent": 84.01,
  "status": "Warning",
  "expenseCount": 23,
  "categories": [
    { "category": "Food", "total": 3150.00, "limit": 3000.00, "status": "Exceeded" },
    { "category": "School", "total": 0, "limit": null, "status": "NoLimit" }
  ],
  "recentExpenses": [ "...up to 5 Expense objects, newest expenseDate first..." ],
  "recentSavings": [ "...up to 5 savings moves in the period, newest first: { id, goalId, goalName, type, amount, date, note, createdAt }..." ]
}
```

- `budget` is `null` when the period has no budget. In that case `remaining` and `percentSpent` are `null`, and `status` is `"NoBudget"`.
- `saved` is the money moved into savings goals inside the period: deposits minus withdrawals, never below 0 (withdrawing money saved earlier does not add to a budget). It is not spending, so `totalSpent` and the categories leave it out, but it comes out of the budget: `remaining = budget − totalSpent − saved` and `percentSpent = (totalSpent + saved) / budget`.
- `status` is `"OnTrack"` below 80%, `"Warning"` from 80% up to 100%, and `"Exceeded"` above 100%, of that same spent-plus-saved amount.
- `categories` always has all six categories. Each one's `status` is `"NoLimit"`, `"OnTrack"`, `"Warning"` or `"Exceeded"`.

### Budgets
| Method | Route | Body | Success |
|---|---|---|---|
| GET | `/api/budgets?month=YYYY-MM` | | 200, every budget starting in that month, any period (`month` is optional) |
| GET | `/api/budgets?period=Weekly&date=YYYY-MM-DD` | | 200, the budget for the week containing that date (an empty array if none) |
| GET | `/api/budgets/{id}` | | 200 Budget, or 404 |
| POST | `/api/budgets` | `{ period, start, amount, categoryLimits[] }` | 201 Budget, or 409 if that period already has one |
| PUT | `/api/budgets/{id}` | `{ period, start, amount, categoryLimits[] }` | 200 Budget |
| DELETE | `/api/budgets/{id}` | | 204; the period has no budget again (expenses are not touched) |

`period` defaults to `Monthly`. A monthly budget may send `month` instead of `start`. `start` must
be the first day of the period: any day for Daily, a Monday for Weekly, the 1st for Monthly.

Validation: each category can appear at most once in `categoryLimits`, and all the limits together cannot add up to more than `amount`.

When an expense takes a category over its limit, the app offers to cover the difference: it sends a `PUT` that raises that limit and lowers another category's (which keeps at least ₱1), or uses budget not given to any category. The budget's `amount` never changes.

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
stored or typed in. Saving does not count as spending, but money moved into savings comes out
of the budget for the day, week or month it is dated in (see `saved` on the dashboard summary).

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
`GET /api/allowance?month=YYYY-MM` or `?period=Weekly&date=YYYY-MM-DD`

```json
{ "period": "Weekly", "start": "2026-11-02", "end": "2026-11-08", "status": "Available",
  "budget": 1000, "spent": 250, "saved": 0, "reservedForBills": 0, "available": 750,
  "daysLeft": 7, "daily": 107.14, "total": 750 }
```

`available = budget − spent in the period − moved into savings in the period − unpaid bills
due in the period` (overdue ones included). `daily` is what is left divided by the days left in the period (today included,
Manila time), rounded down to the centavo; `total` is everything left. Only that period's own
budget is used. `status` is `Available`, `OverBudget`, `Ended` or `NoBudget`.

### Insights
`GET /api/insights?month=YYYY-MM` (or `?period=Weekly&date=YYYY-MM-DD`) returns rule-based notes for that period, most urgent first:
`{ key, severity: "Critical" | "Warning" | "Success" | "Info", kind: "Fact" | "Suggestion",
title, message, link, dismissible }`. Critical ones (overdue bills, budget exceeded) cannot be
dismissed. Dismissals are remembered in the browser, per account.

### Monthly report
- `GET /api/reports/monthly?month=YYYY-MM`: budget, total spent, comparison with the
  previous month, categories, savings, bills, insights and notes.
- `GET /api/reports/monthly.csv?month=YYYY-MM`: the same report as a CSV download (UTF-8).

The report's budget is the month's own monthly budget. Weekly and daily budgets starting in the
month are listed separately (`periodBudgets`: period, start, end, amount, the spending and the
money moved into savings inside their own dates, and `left = amount − spent − saved`); they are
never added up into a monthly budget. `budgetUsedPercent` and `remaining` count spending plus the
month's net savings deposits.

`transactions` lists every expense and savings move dated in the month, oldest first: `{ date, kind, description, category, amount }`, where `kind` is `Expense`, `SavingsDeposit` or `SavingsWithdrawal`, and `category` is the goal name for a savings move. The page groups them by day, week or month. The CSV has the same list.

Paid bills recorded as expenses are counted once, in spending. Income is not recorded in
PennyWise, so the report shows no income or net cash flow.

### Health
`GET /api/health` returns `200 { "status": "ok" }`. Milestone 7 uses it to show
that EC2 #1 can reach EC2 #2.
