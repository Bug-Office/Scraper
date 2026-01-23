using Microsoft.AspNetCore.Mvc;

namespace Scraper.Api.Controllers;

public class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        return Redirect("/index.html");
    }
}

