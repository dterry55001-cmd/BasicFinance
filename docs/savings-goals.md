# Savings Goals and Emergency Fund Planning

## Scope

The Savings module tracks personal savings goals independently from budget allocations and account balances. Each goal has a target amount, start date, optional target date, priority, and USD currency.

## Savings activity

- Contributions increase a goal's saved amount.
- Withdrawals reduce the saved amount and cannot exceed the current saved balance.
- Activity is recorded with a date and optional notes.
- Goals can be edited, paused, resumed, or archived. Paused and archived goals cannot receive new activity.
- Completed goals can still record contributions or withdrawals; their status returns to Active if the saved balance falls below the target.

The contribution ledger is the source of truth for goal progress. Each entry may optionally link to one existing financial transaction. Linked transactions must be unlinked, use the same currency, match the amount and date, and have a compatible type (income/deposit for contributions; expense/withdrawal for withdrawals). A transaction can be linked to only one savings entry. Manual entries do not create or modify bank transactions or account balances.

## Progress and estimates

Progress is calculated as net contributions divided by the goal target, capped at 100%. The remaining amount cannot fall below zero.

Estimated completion uses the average net contribution across the latest three calendar months. An estimate is not displayed until there is a positive average contribution rate.

## Performance reporting

The dashboard summarizes total saved amounts and target allocations, and reports monthly contributions, withdrawals, and net savings for the latest six calendar months.

## Monthly allocation targets

Each goal can have one target per calendar month. Monthly progress compares the goal's net contributions in that month with the target amount. The combined monthly target is shown on the Savings dashboard.

## Emergency fund planning

The emergency fund plan stores essential monthly expenses and a target coverage period from 1 to 24 months. The target reserve is calculated as:

`Essential monthly expenses × target coverage months`

The plan creates or updates a dedicated Emergency Fund goal. Coverage is calculated from the recorded emergency-fund balance divided by essential monthly expenses.

## Milestones

Milestones are recorded once at 25%, 50%, 75%, and 100% of the goal target. Reaching a milestone displays a confirmation message and stores the milestone history.

## Data ownership

- `SavingsGoals`: goal definitions and status.
- `SavingsContributions`: contribution and withdrawal ledger.
- `SavingsMonthlyTargets`: per-goal monthly allocation targets.
- `SavingsGoalMilestones`: achieved milestone history.
- `EmergencyFundPlans`: essential expense and coverage assumptions.

All amounts are currently USD. Transaction links provide traceability only; they do not automatically synchronize savings ledger entries with account balances. Users remain responsible for recording the underlying financial transaction separately.


## Savings automation and allocation

### Recurring contribution schedules

A recurring schedule defines a goal, amount, frequency (weekly, biweekly, monthly, quarterly, or yearly), next due date, optional end date, and active status. Due schedules are surfaced on the goal dashboard. Past-due schedules are labeled as missed contributions.

A due occurrence is posted to the savings ledger only after the user confirms it. Confirmation advances the schedule to its next occurrence, and the schedule stops after its end date or when the goal is completed. Each schedule/due-date pair is unique, preventing duplicate ledger entries if a confirmation is retried. Deactivating a schedule stops future occurrences but preserves its contribution history.

This is intentional confirmation-based automation: it does not create a bank transaction, transfer money, or alter account balances. The user records or links the actual financial transaction separately when appropriate.

### Monthly allocation planning

The allocation planner stores a monthly savings budget and a percentage allocation for each goal. Allocation percentages across non-archived USD goals cannot exceed 100%. Planned amounts are rounded to cents and calculated as:

`Monthly savings budget × goal allocation percentage`

The planner displays allocated percentages, planned amounts, and unallocated budget. These are recommendations only; they do not automatically fund goals or create ledger entries. Existing monthly savings targets remain separate progress targets.

### Funding recommendations

For active goals with a target date and a remaining balance, the dashboard estimates the monthly contribution needed to reach the goal by its deadline. Recommendations are ordered by goal priority and target date. The planner compares the required monthly contribution with the goal's planned allocation and displays any additional monthly amount needed. Goals without a target date are excluded because there is no deadline from which to calculate a required monthly amount.


## Savings notifications and financial integration

The Savings alerts page stores in-app notifications for:
- upcoming recurring contributions within the configured 0–30 day reminder window
- overdue recurring contributions that remain unconfirmed
- recently achieved savings goal milestones
- emergency fund coverage below the configured 0.5–24 month threshold
- a monthly allocation review during the first seven days of each month

Notification preferences are stored in a single global settings row, consistent with the current single-user/local-first application design. Each notification is deduplicated with a deterministic key and a unique database index. Users can mark alerts individually or mark all alerts as read.

Eligible notifications are generated by a hosted background worker when the application starts and every six hours while it is running. Opening the Savings alerts page also triggers generation so users can refresh alerts immediately. Deterministic notification keys and a unique database index prevent duplicate records when the page and worker evaluate the same event concurrently. The worker logs failures and retries at its next interval; it does not send email, SMS, browser push, or mobile push notifications. Generation therefore depends on the application being running, and there is no guarantee of delivery while the application is stopped.

### Financial integration safeguards

Savings reminders and recommendations do not create transactions, move money, or change account balances. A recurring contribution remains a user-confirmed savings ledger entry. Where an actual financial transaction exists, users can link it through the existing transaction-linking workflow. The Savings dashboard surfaces exact, unambiguous match suggestions for unlinked savings entries and unlinked transactions with the same amount, date, currency, and compatible direction. Suggestions are shown only when the transaction maps to exactly one savings entry and that savings entry maps to exactly one transaction; ambiguous candidates are not auto-selected and can be reviewed through account reconciliation. Users must explicitly confirm each suggested link. For goals with a linked account, income/deposits and expenses/withdrawals must affect that account, while transfers are interpreted by source and destination account. A transfer into the linked account can match a contribution; a transfer out can match a withdrawal. Goals without a linked account can match non-transfer activity only. Each transaction can be linked to only one savings contribution.


## Savings reporting and analytics

The Savings reports page provides a selected-month report and a calendar-year summary for USD goals that are not archived. It reports:
- monthly planned target versus actual recorded contributions
- withdrawals and net savings
- contribution variance against the monthly target
- closing savings balance and goal progress
- estimated completion date where recent net contribution activity supports an estimate
- monthly annual-summary totals and trailing 12-month emergency-fund balances

The selected month can be exported as UTF-8 CSV. Text fields are quoted and embedded quotes are escaped. Report actuals exclude future-dated ledger entries. For the current month, actual activity is month-to-date while the planned target is the full-month target; the displayed variance is preliminary. The variance is defined as actual contributions minus planned target. Withdrawals are displayed separately and are not subtracted from this target variance.

Reports include active, paused, and completed goals, while archived goals are excluded. A goal's closing balance is the recorded contributions less recorded withdrawals through the report period's end date (capped at today). The emergency fund trend uses the selected month as its anchor unless that month is in the future, in which case the current month is used.

CSV export is intended for spreadsheet use and contains the selected month's goal-level rows. Text fields are quoted, embedded quotes are escaped, and formula-like text prefixes are neutralized to reduce spreadsheet formula-injection risk. It does not include account balances or create accounting transactions.

The reports view also includes a comparative monthly bar chart for planned targets and actual contributions, plus a trailing emergency-fund balance chart. Accessible tabular values remain available alongside the charts so the information is not conveyed by color or bar length alone.


## Savings account reconciliation

Each active USD savings goal can be linked to one active USD checking, savings, or cash account. An active account can be linked to only one non-archived savings goal. This avoids treating one bank balance as the balance of multiple goals.

A reconciliation session records a date range, the statement closing balance supplied by the user, and the calculated account balance. The calculated balance uses the account opening balance plus all recorded financial transactions through the period end, following the application's account-balance rules for deposits, withdrawals, income, expenses, and transfers. The displayed difference is statement balance minus calculated balance.

The session compares goal contribution and withdrawal entries with transactions that affect the linked account. Incoming transfers are treated as contributions to the destination account; outgoing transfers are treated as withdrawals from the source account. Exact existing transaction links are recognized. Unlinked entries with matching amount, date, and direction are presented for explicit user confirmation. Ambiguous same-day/same-amount matches are flagged as potential duplicates rather than silently linked.

Confirming a match links the existing transaction to the savings entry, preserving the one-transaction-per-contribution database constraint. It does not create, edit, or delete a financial transaction, contribution, or transfer. Entries already linked elsewhere cannot be reused. Reconciliation sessions preserve their results as history, and users can review the balance difference and all matched or unresolved items later.

The balance comparison is account-level; it is not a separate cash sub-ledger for an individual goal. If an account holds funds for several purposes, the one-goal-per-account link rule means those purposes should use distinct accounts or remain unlinked until an appropriate allocation design is added. A zero account balance difference does not imply every savings ledger item is matched. Reconciliation is a review aid and does not replace a bank statement or accounting review.
