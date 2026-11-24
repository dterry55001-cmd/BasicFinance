# Account types

BasicFinance groups tracked accounts into Checking, Savings, Cash, Credit Card, Brokerage, Retirement, and Debt.

## Debt account types

When Debt is selected, the account form requires one subtype:

- Credit card
- Mortgage
- Home equity loan
- Home equity line of credit (HELOC)
- Auto loan
- Student loan
- Personal loan
- Business loan
- Medical debt
- Buy now, pay later
- Other line of credit
- Tax debt
- Other debt

The subtype is descriptive metadata; balances and transactions continue to use the account's normal ledger balance. Debt account opening balances should be entered as positive amounts owed. Do not enter a negative opening balance simply because the account is a liability.

The account stores only the last four account-number digits. Do not enter full account numbers or credentials.

## Retirement account types

Retirement accounts have their own subtype list, including employer-sponsored plans, IRAs, pensions, annuities, and other retirement accounts.

## Data model

Account subtype values are nullable and only populated for the matching parent account category. Changing an account to a different category clears the old subtype. Existing account categories and records remain compatible.
