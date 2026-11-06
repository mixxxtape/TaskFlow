using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Web.Controllers;

public class GameController : Controller
{
    public IActionResult Index() => View();
}