using BasicFinance.Web.Models;

namespace BasicFinance.Web.Services;

/// <summary>
/// Projects a savings goal without creating ledger entries or changing saved schedules.
/// </summary>
public sealed class SavingsGoalProjectionCalculator
{
    private readonly SavingsGoalService _scheduleService = new();

    public SavingsGoalProjectionResult Calculate(
        decimal currentBalance,
        decimal targetAmount,
        string currencyCode,
        IEnumerable<RecurringSavingsContribution> recurringSchedules,
        IEnumerable<PlannedSavingsProjectionEvent>? plannedEvents,
        DateTime asOfDate,
        int projectionMonths = 24)
    {
        ArgumentNullException.ThrowIfNull(recurringSchedules);
        if (currentBalance < 0m) throw new ArgumentOutOfRangeException(nameof(currentBalance));
        if (targetAmount <= 0m) throw new ArgumentOutOfRangeException(nameof(targetAmount));
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3 ||
            currencyCode.Any(c => !char.IsAsciiLetterUpper(c)))
            throw new ArgumentException("Currency code must be three uppercase letters.", nameof(currencyCode));
        if (projectionMonths is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(projectionMonths));

        var events = (plannedEvents ?? []).ToArray();
        if (events.Any(e => e.Amount <= 0m || !Enum.IsDefined(e.Type)))
            throw new ArgumentException("Planned events require a positive amount and valid event type.", nameof(plannedEvents));

        var scheduleStates = recurringSchedules.Where(s => s.IsActive).Select(schedule =>
        {
            if (schedule.Amount <= 0m || !Enum.IsDefined(schedule.Frequency))
                throw new ArgumentException("Active schedules require a positive amount and valid frequency.", nameof(recurringSchedules));
            return new ScheduleState(schedule, schedule.NextDueDate.Date);
        }).ToList();

        var start = new DateTime(asOfDate.Year, asOfDate.Month, 1).AddMonths(1);
        var balance = currentBalance;
        DateTime? completionDate = balance >= targetAmount ? asOfDate.Date : null;
        var rows = new List<SavingsGoalProjectionMonth>(projectionMonths);

        for (var i = 0; i < projectionMonths; i++)
        {
            var monthStart = start.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1);
            var opening = balance;
            var contributions = 0m;
            var withdrawals = 0m;

            foreach (var state in scheduleStates)
            {
                var endDate = state.Schedule.EndDate?.Date;
                if (endDate.HasValue && (endDate.Value < monthStart || state.NextDueDate > endDate.Value))
                    continue;

                while (state.NextDueDate < monthStart &&
                       (!endDate.HasValue || state.NextDueDate <= endDate.Value))
                    state.NextDueDate = _scheduleService.GetNextSavingsDueDate(state.NextDueDate, state.Schedule.Frequency);

                while (state.NextDueDate >= monthStart && state.NextDueDate < monthEnd &&
                       (!endDate.HasValue || state.NextDueDate <= endDate.Value))
                {
                    contributions += state.Schedule.Amount;
                    state.NextDueDate = _scheduleService.GetNextSavingsDueDate(state.NextDueDate, state.Schedule.Frequency);
                }
            }

            foreach (var item in events.Where(e => e.EventDate.Date >= monthStart && e.EventDate.Date < monthEnd))
            {
                if (item.Type == SavingsProjectionEventType.Contribution) contributions += item.Amount;
                else withdrawals += item.Amount;
            }

            balance = Math.Max(0m, balance + contributions - withdrawals);
            if (!completionDate.HasValue && balance >= targetAmount)
                completionDate = monthEnd.AddDays(-1);

            rows.Add(new SavingsGoalProjectionMonth(
                monthStart, opening, contributions, withdrawals, balance,
                Math.Clamp(balance / targetAmount * 100m, 0m, 100m),
                Math.Max(0m, targetAmount - balance), balance >= targetAmount));
        }

        return new SavingsGoalProjectionResult(
            currentBalance, targetAmount, completionDate, currencyCode, rows);
    }

    private sealed class ScheduleState(RecurringSavingsContribution schedule, DateTime nextDueDate)
    {
        public RecurringSavingsContribution Schedule { get; } = schedule;
        public DateTime NextDueDate { get; set; } = nextDueDate;
    }
}
