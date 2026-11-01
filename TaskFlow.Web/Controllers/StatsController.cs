using Microsoft.AspNetCore.Mvc;
using TaskFlow.Web.Services;

namespace TaskFlow.Web.Controllers;

public class StatsController : Controller
{
    private readonly StatsService _stats;
    public StatsController(StatsService stats) => _stats = stats;

    public IActionResult Index() => View();

    public async Task<IActionResult> Data() => Json(await _stats.GetAsync());
}
