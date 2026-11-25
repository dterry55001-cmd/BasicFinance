using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class DebtNotification
{
    public int Id { get; set; }
    [Required, StringLength(64)]
    public string NotificationKey { get; set; } = string.Empty;
    [Required, StringLength(20)]
    public string Level { get; set; } = "Information";
    [Required, StringLength(60)]
    public string Category { get; set; } = string.Empty;
    [Required, StringLength(500)]
    public string Message { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
