namespace BasicFinance.Web.Models;

/// <summary>Append-only record of an evaluation of a forecast snapshot against actual ledger data.</summary>
public sealed class CashFlowForecastEvaluation
{
    public int Id { get; set; }
    public int ForecastSnapshotId { get; set; }
    public DateTime EvaluatedUtc { get; set; }
    public bool ActualDataComplete { get; set; }
    public string ResultsJson { get; set; } = "[]";
}
