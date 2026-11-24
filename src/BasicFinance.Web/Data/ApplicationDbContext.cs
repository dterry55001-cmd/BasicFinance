using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Data;

/// <summary>Empty persistence boundary for future finance modules.</summary>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
}
