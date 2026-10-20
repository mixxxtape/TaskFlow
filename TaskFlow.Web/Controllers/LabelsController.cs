using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Data;
using TaskFlow.Web.Models;

namespace TaskFlow.Web.Controllers;

public class LabelsController : Controller
{
    private readonly AppDbContext _db;
    public LabelsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index() =>
        View(await _db.Labels.OrderBy(l => l.Name).ToListAsync());

    public IActionResult Create() => View(new Label());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Color")] Label label)
    {
        if (!ModelState.IsValid) return View(label);
        _db.Labels.Add(label);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();
        var label = await _db.Labels.FindAsync(id);
        return label is null ? NotFound() : View(label);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Color")] Label input)
    {
        if (id != input.Id) return NotFound();
        if (!ModelState.IsValid) return View(input);

        var label = await _db.Labels.FindAsync(id);
        if (label is null) return NotFound();
        label.Name = input.Name;
        label.Color = input.Color;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();
        var label = await _db.Labels.FindAsync(id);
        return label is null ? NotFound() : View(label);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var label = await _db.Labels.FindAsync(id);
        if (label is not null)
        {
            _db.Labels.Remove(label);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
