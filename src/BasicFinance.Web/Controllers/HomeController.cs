using Microsoft.AspNetCore.Mvc;

namespace BasicFinance.Web.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => View();

    public IActionResult Error() => View();
}
