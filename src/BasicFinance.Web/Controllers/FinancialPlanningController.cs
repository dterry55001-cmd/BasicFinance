using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BasicFinance.Web.Controllers;

public sealed class FinancialPlanningController(FinancialPlanningDashboardService dashboardService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? asOfDate, string reportingCurrency = "USD", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportingCurrency) || reportingCurrency.Length != 3 ||
            reportingCurrency.Any(c => !char.IsAsciiLetterUpper(c)))
            return BadRequest("Reporting currency must be a three-letter uppercase code.");

        var date = asOfDate ?? DateOnly.FromDateTime(DateTime.Today);
        var model = await dashboardService.BuildAsync(date, reportingCurrency, cancellationToken);
        return View(model);
    }
}
