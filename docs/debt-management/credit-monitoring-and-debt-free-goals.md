# Credit Monitoring and Debt-Free Goals

## Scope

This feature set extends the existing debt account, account balance history, debt payment, and payoff progress functionality. It must not duplicate the transaction ledger or replace account balances.

## 1. Credit monitoring

### Credit limit history
- Keep the current credit limit on the account for current utilization calculations.
- Add an append-only credit limit history record whenever a credit limit is created or changed.
- Record account, previous limit, new limit, effective date, recorded timestamp, source (manual or statement/report), and optional notes.
- Editing an account's current credit limit must create a history entry in the same database transaction.
- Do not rewrite prior history when a limit changes.
- Support an initial limit record when a credit-card or revolving-credit account is first assigned a limit.

### Reported balances
- Store dated reported-balance snapshots separately from ledger balances and account balance snapshots.
- Record account, reporting date, reported balance, reported credit limit when supplied, source/bureau, received date, and optional notes.
- Allow multiple reported-balance snapshots for the same account on different dates; prevent accidental duplicate submissions using a suitable account/date/source uniqueness rule.
- Do not alter the account's ledger balance when a reported balance is entered.
- Calculate reported utilization from reported balance and the reported credit limit for that snapshot, falling back to the account's limit only when the snapshot has no reported limit. Clearly identify which limit was used.

### Credit inquiries
- Record inquiry date, bureau, lender/creditor, inquiry type (hard or soft), and optional notes.
- Inquiries are manually entered; do not imply access to credit bureaus or automatic credit-report monitoring.
- Provide list, create, edit, and delete workflows with date and bureau filtering.

### Utilization reporting
- Show current utilization per revolving-credit account and aggregate utilization by currency.
- Show historical utilization trends from dated reported-balance snapshots and limit history.
- Use only revolving accounts with a positive applicable credit limit in utilization denominators. Do not combine currencies.
- Handle zero/missing limits without division errors and label utilization as unavailable.
- Do not treat installment debt as revolving utilization.

## 2. Debt-free milestones and goals

### Debt-free target
- Allow a user to set an overall target debt-free date and optional notes.
- Preserve the target independently of calculated estimates.
- Show variance between the target and the latest estimated payoff date.

### Monthly debt reduction goals
- Allow a monthly target reduction amount per currency, with start month, optional end month, and notes.
- Define reduction consistently as the change in total outstanding debt principal between the month opening baseline and month-end balance. Payments alone are not the reduction amount because interest and fees can offset principal reduction.
- Compare actual reduction with the goal and show amount remaining or exceeded.
- Do not combine balances across currencies.

### Individual debt-free milestones
- Detect when an active debt account's calculated balance reaches zero or below.
- Create a milestone linked to the account, with the achieved date and balance evidence.
- Prevent duplicate milestones for the same account.
- Preserve the milestone if the account is later archived; do not silently delete history.
- If a correction or reversal makes a previously paid-off account outstanding again, retain the historical milestone and flag it as reopened rather than erasing it.

### Overall payoff estimate
- Reuse the existing debt payoff calculation service and account APR/minimum-payment data.
- Estimate separately by currency; never sum or schedule cross-currency debt as one amount.
- Include all active debt and credit-card accounts with positive balances.
- Clearly disclose assumptions: payment amounts, minimum-payment fallback, interest compounding convention, payment timing, and whether extra monthly payments are included.
- If a debt lacks a usable rate or payment amount, show an incomplete estimate and identify the missing inputs instead of presenting a misleading date.
- Refresh estimates when balances, APRs, minimum payments, or planned monthly payment amounts change.
- Distinguish a calculated estimate from the user's saved target debt-free date.

## 3. Navigation and reporting

Add a Credit Monitoring area with:
- Credit limit history
- Reported balances
- Credit inquiries
- Utilization overview and trend

Extend Debt Planning with:
- Overall debt-free target
- Monthly reduction goals and actuals
- Individual debt-free milestones
- Estimated overall payoff date

Link to existing account details, balance history, debt payment calendar, and payoff progress. Avoid duplicating those workflows.

## 4. Data and security constraints

- BasicFinance remains standalone. Data is entered manually or through user-initiated statement imports.
- Do not add bank connections, credit-bureau APIs, account credentials, routing numbers, or full bank account numbers.
- Credit-report details are user-entered records, not verified bureau data.
- Store monetary values using the application's existing SQLite decimal/text convention.
- Use EF Core migrations and add indexes/foreign-key delete restrictions consistent with existing account history records.

## 5. Acceptance criteria

1. Changing a credit limit preserves the prior limit and effective date.
2. Utilization is correct for each revolving account and for each currency group.
3. Reported-balance entries do not change the ledger balance.
4. Historical utilization uses the limit applicable to the reported snapshot and clearly identifies fallback limits.
5. Hard and soft inquiries can be recorded and filtered without any external bureau integration.
6. A monthly debt-reduction goal compares opening and closing principal balances, not gross payment totals.
7. A debt reaching zero creates exactly one dated milestone; reopening does not erase the historical achievement.
8. The overall payoff estimate uses existing debt payment calculations and exposes missing assumptions.
9. Saved target dates remain distinct from estimated payoff dates.
10. EF Core migration, controller/service tests, validation tests, and UI workflows cover the new features.
