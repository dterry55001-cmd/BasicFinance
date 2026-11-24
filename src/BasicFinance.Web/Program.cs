using BasicFinance.Web.Data;
using BasicFinance.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<AccountBalanceService>();
builder.Services.AddScoped<CsvStatementParser>();
builder.Services.AddScoped<InstitutionStatementParser>();
builder.Services.AddScoped<StatementImportPreparationService>();
builder.Services.AddScoped<TransactionCategorySeeder>();
builder.Services.AddScoped<TransactionRuleService>();
builder.Services.AddScoped<DebtManagementService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

var databaseDirectory = Path.Combine(app.Environment.ContentRootPath, "data");
Directory.CreateDirectory(databaseDirectory);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await database.Database.MigrateAsync();
    var categorySeeder = scope.ServiceProvider.GetRequiredService<TransactionCategorySeeder>();
    await categorySeeder.SeedAsync();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
