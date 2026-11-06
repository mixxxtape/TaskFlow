using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Web.Controllers;

public class EditorController : Controller
{
    public IActionResult Index() => View();
}