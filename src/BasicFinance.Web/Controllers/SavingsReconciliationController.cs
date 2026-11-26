using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class SavingsReconciliationController(
    ApplicationDbContext database,
    AccountBalanceService accountBalances,
    SavingsGoalService savingsService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var goals = await database.SavingsGoals.AsNoTracking()
            .Where(x => x.CurrencyCode == "USD" && x.Status != SavingsGoalStatus.Archived)
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var accounts = await database.Accounts.AsNoTracking()
            .Where(x => x.CurrencyCode == "USD" && x.Status == AccountStatus.Active
                && (x.Type == AccountType.Savings || x.Type == AccountType.Checking || x.Type == AccountType.Cash))
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);
        foreach (var goal in goals)
            goal.LinkedAccount = accounts.FirstOrDefault(x => x.Id == goal.LinkedAccountId);
        var sessions = await database.SavingsReconciliationSessions.AsNoTracking()
            .Include(x => x.SavingsGoal).Include(x => x.Account)
            .OrderByDescending(x => x.CreatedUtc).Take(50).ToListAsync(cancellationToken);
        return View(new SavingsReconciliationIndexViewModel
        {
            Goals = goals,
            Accounts = accounts,
            Sessions = sessions
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkAccount(int goalId, int? accountId, CancellationToken cancellationToken)
    {
        var goal = await database.SavingsGoals.FirstOrDefaultAsync(x => x.Id == goalId
            && x.CurrencyCode == "USD" && x.Status != SavingsGoalStatus.Archived, cancellationToken);
        if (goal is null) return NotFound();

        if (accountId.HasValue)
        {
            var account = await database.Accounts.FirstOrDefaultAsync(x => x.Id == accountId.Value
                && x.CurrencyCode == goal.CurrencyCode && x.Status == AccountStatus.Active
                && (x.Type == AccountType.Savings || x.Type == AccountType.Checking || x.Type == AccountType.Cash), cancellationToken);
            if (account is null)
            {
                TempData["ErrorMessage"] = "Choose an active USD savings, checking, or cash account.";
                return RedirectToAction(nameof(Index));
            }

            var alreadyLinked = await database.SavingsGoals.AnyAsync(x => x.Id != goalId
                && x.LinkedAccountId == account.Id && x.Status != SavingsGoalStatus.Archived, cancellationToken);
            if (alreadyLinked)
            {
                TempData["ErrorMessage"] = "That account is already linked to another active savings goal. Each account can be linked to one goal to avoid duplicate balances.";
                return RedirectToAction(nameof(Index));
            }
        }

        goal.LinkedAccountId = accountId;
        goal.UpdatedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = accountId.HasValue ? "Savings account linked to goal." : "Savings account link removed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(int goalId, DateTime periodStart, DateTime periodEnd,
        decimal statementClosingBalance, CancellationToken cancellationToken)
    {
        var goal = await database.SavingsGoals.FirstOrDefaultAsync(x => x.Id == goalId && x.CurrencyCode == "USD"
            && x.Status != SavingsGoalStatus.Archived, cancellationToken);
        if (goal is null) return NotFound();
        var account = goal.LinkedAccountId.HasValue
            ? await database.Accounts.FirstOrDefaultAsync(x => x.Id == goal.LinkedAccountId.Value, cancellationToken)
            : null;
        if (account is null || account.Status != AccountStatus.Active
            || account.CurrencyCode != goal.CurrencyCode
            || account.Type is not (AccountType.Savings or AccountType.Checking or AccountType.Cash))
        {
            TempData["ErrorMessage"] = "Link an active USD financial account to this goal before reconciling.";
            return RedirectToAction(nameof(Index));
        }
        if (periodStart.Date > periodEnd.Date || periodEnd.Date > DateTime.Today
            || (periodEnd.Date - periodStart.Date).TotalDays > 366
            || statementClosingBalance < -999999999999m || statementClosingBalance > 999999999999m)
        {
            TempData["ErrorMessage"] = "Enter a valid reconciliation period of no more than 367 calendar days, ending no later than today, and a valid closing balance.";
            return RedirectToAction(nameof(Index));
        }

        var accountId = account.Id;
        var transactions = await database.Transactions.AsNoTracking()
            .Where(x => x.CurrencyCode == goal.CurrencyCode && x.TransactionDate >= periodStart.Date
                && x.TransactionDate < periodEnd.Date.AddDays(1)
                && (x.AccountId == accountId || x.RelatedAccountId == accountId))
            .OrderBy(x => x.TransactionDate).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var contributions = await database.SavingsContributions
            .Include(x => x.Transaction)
            .Where(x => x.SavingsGoalId == goalId && x.ContributionDate >= periodStart.Date
                && x.ContributionDate < periodEnd.Date.AddDays(1))
            .OrderBy(x => x.ContributionDate).ThenBy(x => x.Id).ToListAsync(cancellationToken);

        var session = new SavingsReconciliationSession
        {
            SavingsGoalId = goalId,
            AccountId = accountId,
            PeriodStart = periodStart.Date,
            PeriodEnd = periodEnd.Date,
            StatementClosingBalance = statementClosingBalance,
            CalculatedAccountBalance = await accountBalances.GetBalanceAtAsync(accountId, periodEnd.Date.AddDays(1).AddTicks(-1), cancellationToken),
            CreatedUtc = DateTime.UtcNow
        };
        session.Difference = statementClosingBalance - session.CalculatedAccountBalance;

        var usedContributionIds = new HashSet<int>();
        var linkedTransactionIds = await database.SavingsContributions.AsNoTracking()
            .Where(x => x.TransactionId.HasValue)
            .Select(x => x.TransactionId!.Value)
            .ToHashSetAsync(cancellationToken);
        foreach (var transaction in transactions)
        {
            var direction = GetDirection(transaction, accountId);
            if (direction is null) continue;
            var linked = contributions.FirstOrDefault(x => x.TransactionId == transaction.Id);
            if (linked is not null)
            {
                var valid = savingsService.IsCompatibleTransaction(transaction,
                    direction == SavingsContributionType.Contribution ? SavingsContributionType.Contribution : SavingsContributionType.Withdrawal,
                    linked.Amount, linked.ContributionDate, goal.CurrencyCode)
                    && linked.Type == direction;
                session.Items.Add(new SavingsReconciliationItem
                {
                    TransactionId = transaction.Id,
                    SavingsContributionId = linked.Id,
                    Status = valid ? SavingsReconciliationItemStatus.Matched : SavingsReconciliationItemStatus.AlreadyLinked,
                    Amount = transaction.Amount,
                    EventDate = transaction.TransactionDate.Date,
                    Description = transaction.Description
                });
                if (valid) usedContributionIds.Add(linked.Id);
                continue;
            }

            var candidates = contributions.Where(x => !x.TransactionId.HasValue && !usedContributionIds.Contains(x.Id)
                && x.Type == direction && x.Amount == transaction.Amount
                && x.ContributionDate.Date == transaction.TransactionDate.Date).ToList();
            var status = linkedTransactionIds.Contains(transaction.Id)
                ? SavingsReconciliationItemStatus.AlreadyLinked
                : candidates.Count > 1
                    ? SavingsReconciliationItemStatus.PotentialDuplicate
                    : SavingsReconciliationItemStatus.UnmatchedTransaction;
            session.Items.Add(new SavingsReconciliationItem
            {
                TransactionId = transaction.Id,
                Status = status,
                Amount = transaction.Amount,
                EventDate = transaction.TransactionDate.Date,
                Description = transaction.Description
            });
        }

        foreach (var contribution in contributions.Where(x => !usedContributionIds.Contains(x.Id)))
        {
            if (contribution.TransactionId.HasValue)
            {
                if (session.Items.Any(x => x.SavingsContributionId == contribution.Id)) continue;
                session.Items.Add(new SavingsReconciliationItem
                {
                    SavingsContributionId = contribution.Id,
                    Status = SavingsReconciliationItemStatus.AlreadyLinked,
                    Amount = contribution.Amount,
                    EventDate = contribution.ContributionDate.Date,
                    Description = contribution.Notes ?? "Savings entry linked to another transaction"
                });
                continue;
            }

            var candidateCount = transactions.Count(x => GetDirection(x, accountId) == contribution.Type
                && x.Amount == contribution.Amount && x.TransactionDate.Date == contribution.ContributionDate.Date);
            session.Items.Add(new SavingsReconciliationItem
            {
                SavingsContributionId = contribution.Id,
                Status = candidateCount > 1 ? SavingsReconciliationItemStatus.PotentialDuplicate
                    : SavingsReconciliationItemStatus.UnmatchedContribution,
                Amount = contribution.Amount,
                EventDate = contribution.ContributionDate.Date,
                Description = contribution.Notes ?? (contribution.Type == SavingsContributionType.Contribution ? "Savings contribution" : "Savings withdrawal")
            });
        }

        session.MatchedCount = session.Items.Count(x => x.Status == SavingsReconciliationItemStatus.Matched);
        session.UnmatchedTransactionCount = session.Items.Count(x => x.Status == SavingsReconciliationItemStatus.UnmatchedTransaction);
        session.UnmatchedContributionCount = session.Items.Count(x => x.Status == SavingsReconciliationItemStatus.UnmatchedContribution);
        session.PotentialDuplicateCount = session.Items.Count(x => x.Status == SavingsReconciliationItemStatus.PotentialDuplicate);
        database.SavingsReconciliationSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Reconciliation completed. {session.MatchedCount} matched, {session.UnmatchedTransactionCount} unmatched transactions, {session.UnmatchedContributionCount} unmatched savings entries, and {session.PotentialDuplicateCount} potential duplicates.";
        return RedirectToAction(nameof(Details), new { id = session.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var session = await database.SavingsReconciliationSessions.AsNoTracking()
            .Include(x => x.SavingsGoal).Include(x => x.Account)
            .Include(x => x.Items).ThenInclude(x => x.Transaction)
            .Include(x => x.Items).ThenInclude(x => x.SavingsContribution)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (session is null) return NotFound();
        var availableContributions = await database.SavingsContributions.AsNoTracking()
            .Where(x => x.SavingsGoalId == session.SavingsGoalId && x.TransactionId == null)
            .OrderByDescending(x => x.ContributionDate).ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        return View(new SavingsReconciliationDetailsViewModel
        {
            Session = session,
            AvailableContributions = availableContributions
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Match(int sessionId, int transactionId, int contributionId, CancellationToken cancellationToken)
    {
        var session = await database.SavingsReconciliationSessions.Include(x => x.Items)
            .Include(x => x.SavingsGoal)
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null) return NotFound();
        var item = session.Items.FirstOrDefault(x => x.TransactionId == transactionId
            && x.Status is SavingsReconciliationItemStatus.UnmatchedTransaction or SavingsReconciliationItemStatus.PotentialDuplicate);
        if (item is null) return NotFound();

        var transaction = await database.Transactions.FirstOrDefaultAsync(x => x.Id == transactionId, cancellationToken);
        var contribution = await database.SavingsContributions.FirstOrDefaultAsync(x => x.Id == contributionId
            && x.SavingsGoalId == session.SavingsGoalId, cancellationToken);
        var account = await database.Accounts.FirstOrDefaultAsync(x => x.Id == session.AccountId, cancellationToken);
        if (transaction is null || contribution is null || account is null
            || contribution.TransactionId.HasValue
            || await database.SavingsContributions.AnyAsync(x => x.TransactionId == transactionId, cancellationToken)
            || GetDirection(transaction, session.AccountId) != contribution.Type
            || !savingsService.IsCompatibleTransaction(transaction, contribution.Type, contribution.Amount,
                contribution.ContributionDate, session.SavingsGoal.CurrencyCode)
            || transaction.AccountId != session.AccountId && transaction.RelatedAccountId != session.AccountId)
        {
            TempData["ErrorMessage"] = "The transaction and savings entry must be unlinked and match the account, direction, amount, date, and currency.";
            return RedirectToAction(nameof(Details), new { id = sessionId });
        }

        contribution.TransactionId = transaction.Id;
        item.SavingsContributionId = contribution.Id;
        item.Status = SavingsReconciliationItemStatus.Matched;
        var contributionOnlyItem = session.Items.FirstOrDefault(x =>
            x.SavingsContributionId == contribution.Id && !x.TransactionId.HasValue);
        if (contributionOnlyItem is not null)
        {
            session.Items.Remove(contributionOnlyItem);
            database.SavingsReconciliationItems.Remove(contributionOnlyItem);
        }
        session.MatchedCount = session.Items.Count(x => x.Status == SavingsReconciliationItemStatus.Matched);
        session.UnmatchedTransactionCount = session.Items.Count(x => x.Status == SavingsReconciliationItemStatus.UnmatchedTransaction);
        session.UnmatchedContributionCount = session.Items.Count(x => x.Status == SavingsReconciliationItemStatus.UnmatchedContribution);
        session.PotentialDuplicateCount = session.Items.Count(x => x.Status == SavingsReconciliationItemStatus.PotentialDuplicate);
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Savings entry linked to the matching account transaction.";
        return RedirectToAction(nameof(Details), new { id = sessionId });
    }

    private static SavingsContributionType? GetDirection(FinancialTransaction transaction, int accountId)
    {
        if (transaction.Type == TransactionType.Transfer)
        {
            if (transaction.RelatedAccountId == accountId) return SavingsContributionType.Contribution;
            if (transaction.AccountId == accountId) return SavingsContributionType.Withdrawal;
            return null;
        }
        if (transaction.AccountId != accountId) return null;
        return transaction.Type switch
        {
            TransactionType.Income or TransactionType.Deposit => SavingsContributionType.Contribution,
            TransactionType.Expense or TransactionType.Withdrawal => SavingsContributionType.Withdrawal,
            _ => null
        };
    }
}

public sealed class SavingsReconciliationIndexViewModel
{
    public List<SavingsGoal> Goals { get; set; } = [];
    public List<Account> Accounts { get; set; } = [];
    public List<SavingsReconciliationSession> Sessions { get; set; } = [];
}

public sealed class SavingsReconciliationDetailsViewModel
{
    public SavingsReconciliationSession Session { get; set; } = null!;
    public List<SavingsContribution> AvailableContributions { get; set; } = [];
}
