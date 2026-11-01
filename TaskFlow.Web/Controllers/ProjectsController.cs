using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Data;
using TaskFlow.Web.Models;
using TaskFlow.Web.Services;
using TaskFlow.Web.ViewModels;

namespace TaskFlow.Web.Controllers;

public class ProjectsController : Controller
{
    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;

    public ProjectsController(AppDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IActionResult> Index()
    {
        var projects = await _db.Projects.Include(p => p.Tasks)
            .OrderByDescending(p => p.Id).ToListAsync();
        return View(projects);
    }

    // C6: Details
    public async Task<IActionResult> Details(int? id)
    {
        if (id is null) return NotFound();
        var project = await _db.Projects
            .Include(p => p.Tasks).ThenInclude(t => t.Assignee)
            .Include(p => p.Tasks).ThenInclude(t => t.Labels)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return NotFound();

        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        return View(ProjectLandingViewModel.Create(project, baseUrl));
    }

    public IActionResult Create() => View(new Project());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Description,ContactEmail")] Project project, IFormFile? cover = null)
    {
        string? coverUrl = null;
        if (ModelState.IsValid)
        {
            try { coverUrl = await SaveCover(cover); }
            catch (InvalidOperationException ex) { ModelState.AddModelError("cover", ex.Message); }
        }
        if (!ModelState.IsValid) return View(project);

        project.CoverImageUrl = coverUrl;
        project.CreatedAt = DateTime.UtcNow;
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();
        var project = await _db.Projects.FindAsync(id);
        return project is null ? NotFound() : View(project);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,ContactEmail")] Project input, IFormFile? cover = null)
    {
        if (id != input.Id) return NotFound();
        var project = await _db.Projects.FindAsync(id);
        if (project is null) return NotFound();

        string? coverUrl = null;
        if (ModelState.IsValid)
        {
            try { coverUrl = await SaveCover(cover); }
            catch (InvalidOperationException ex) { ModelState.AddModelError("cover", ex.Message); }
        }
        if (!ModelState.IsValid)
        {
            input.CoverImageUrl = project.CoverImageUrl;
            return View(input);
        }

        var oldCover = project.CoverImageUrl;
        project.Name = input.Name;
        project.Description = input.Description;
        project.ContactEmail = input.ContactEmail;
        if (coverUrl is not null) project.CoverImageUrl = coverUrl;

        await _db.SaveChangesAsync();
        if (coverUrl is not null) _storage.Delete(oldCover);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();
        var project = await _db.Projects.Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id);
        return project is null ? NotFound() : View(project);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var project = await _db.Projects.Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (project is not null)
        {
            var files = project.Tasks.Select(t => t.AttachmentUrl).Append(project.CoverImageUrl).ToList();
            _db.Projects.Remove(project);
            await _db.SaveChangesAsync();
            foreach (var url in files) _storage.Delete(url);
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<string?> SaveCover(IFormFile? cover)
    {
        if (cover is null || cover.Length == 0) return null;
        if (!ImageRules.IsImage(cover.FileName))
            throw new InvalidOperationException("Обкладинка має бути зображенням (png, jpg, gif).");
        return await _storage.SaveAsync(cover);
    }
}
