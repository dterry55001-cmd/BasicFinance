# Savings Goals and Emergency Fund Planning

## Scope

The Savings module tracks personal savings goals independently from budget allocations and account balances. Each goal has a target amount, start date, optional target date, priority, and USD currency.

## Savings activity

- Contributions increase a goal's saved amount.
- Withdrawals reduce the saved amount and cannot exceed the current saved balance.
- Activity is recorded with a date and optional notes.
- Goals can be edited, paused, resumed, or archived. Paused and archived goals cannot receive new activity.
- Completed goals can still record contributions or withdrawals; their status returns to Active if the saved balance falls below the target.

The contribution ledger is the source of truth for goal progress. The optional transaction relationship is reserved for transaction-linked tracking; manual contributions do not create or modify bank transactions.

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

All amounts are currently USD. Savings activity is not automatically synchronized to account balances; transaction-linked contribution workflows can be added without changing the ledger's source-of-truth design.
