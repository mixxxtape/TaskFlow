using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Data;
using TaskFlow.Web.Models;

namespace TaskFlow.Web.Api;

[ApiController]
[Route("api/labels")]
public class LabelsApiController : ControllerBase
{
    private static readonly Expression<Func<Label, LabelDto>> ToDto = l => new LabelDto(l.Id, l.Name, l.Color);

    private readonly AppDbContext _db;
    public LabelsApiController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<PagedResult<LabelDto>>> List(int skip = 0, int limit = Paging.DefaultLimit)
    {
        if (Paging.Validate(skip, limit) is { } error) return Problem(detail: error, statusCode: 400);
        var query = _db.Labels.OrderBy(l => l.Id).Select(ToDto);
        return await Paging.ToPagedAsync(query, skip, limit, Request);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LabelDto>> Get(int id)
    {
        var dto = await _db.Labels.Where(l => l.Id == id).Select(ToDto).FirstOrDefaultAsync();
        return dto is null ? NotFound() : dto;
    }

    [HttpPost]
    public async Task<ActionResult<LabelDto>> Create(LabelInput input)
    {
        var label = new Label { Name = input.Name.Trim(), Color = input.Color };
        _db.Labels.Add(label);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = label.Id }, new LabelDto(label.Id, label.Name, label.Color));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<LabelDto>> Update(int id, LabelInput input)
    {
        var label = await _db.Labels.FindAsync(id);
        if (label is null) return NotFound();
        label.Name = input.Name.Trim();
        label.Color = input.Color;
        await _db.SaveChangesAsync();
        return new LabelDto(label.Id, label.Name, label.Color);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var label = await _db.Labels.FindAsync(id);
        if (label is null) return NotFound();
        _db.Labels.Remove(label);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
