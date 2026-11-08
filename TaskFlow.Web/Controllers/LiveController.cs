using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Web.Controllers;

public class LiveController : Controller
{
    public IActionResult Index() => View();
}