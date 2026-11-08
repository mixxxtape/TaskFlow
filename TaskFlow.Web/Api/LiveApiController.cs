using Microsoft.AspNetCore.Mvc;
using TaskFlow.Web.Services;

namespace TaskFlow.Web.Api;

[ApiController]
[Route("api/live")]
public class LiveApiController : ControllerBase
{
    private readonly LiveState _state;
    private readonly ILiveBroadcaster _broadcaster;

    public LiveApiController(LiveState state, ILiveBroadcaster broadcaster)
    {
        _state = state;
        _broadcaster = broadcaster;
    }

    [HttpGet("latest")]
    public ActionResult<LiveEvent> Latest(long afterId = 0)
    {
        var last = _state.Last;
        if (last is null || last.Id <= afterId) return NoContent();
        return last;
    }

    [HttpPost("ping")]
    public async Task<IActionResult> Ping()
    {
        await _broadcaster.PublishAsync("Тестова подія");
        return Accepted();
    }
}