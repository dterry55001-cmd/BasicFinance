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
