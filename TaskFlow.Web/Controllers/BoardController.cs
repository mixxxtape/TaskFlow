using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Web.Controllers;

public class BoardController : Controller
{
    public IActionResult Index() => View();
}