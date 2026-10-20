using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Data;
using TaskFlow.Web.Models;
using TaskFlow.Web.Services;
using TaskFlow.Web.ViewModels;

namespace TaskFlow.Web.Controllers;

public class TasksController : Controller
{
    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;

    public TasksController(AppDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IActionResult> Index(int? projectId, ItemStatus? status, string? q)
    {
        var query = _db.Tasks
            .Include(t => t.Project).Include(t => t.Assignee).Include(t => t.Labels)
            .AsQueryable();

        if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId.Value);
        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(t => t.Title.ToLower().Contains(term));
        }

        var projects = await _db.Projects.OrderBy(p => p.Name).ToListAsync();
        ViewBag.Projects = new SelectList(projects, "Id", "Name", projectId);
        ViewBag.Q = q;

        return View(await query.OrderByDescending(t => t.Id).ToListAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null) return NotFound();
        var task = await _db.Tasks
            .Include(t => t.Project).Include(t => t.Assignee).Include(t => t.Labels)
            .Include(t => t.Comments).ThenInclude(c => c.Author)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();

        ViewBag.Users = new SelectList(await _db.Users.OrderBy(u => u.Name).ToListAsync(), "Id", "Name");
        return View(task);
    }

    public async Task<IActionResult> Create(int? projectId)
    {
        await LoadLookups();
        return View(new TaskFormViewModel { ProjectId = projectId ?? 0 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TaskFormViewModel vm)
    {
        await ValidateReferences(vm);

        string? uploaded = null;
        if (ModelState.IsValid)
        {
            try { uploaded = await SaveAttachment(vm.Attachment); }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(vm.Attachment), ex.Message);
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadLookups();
            return View(vm);
        }

        var task = new TaskItem();
        Apply(task, vm);
        task.AttachmentUrl = uploaded;
        task.Labels = await _db.Labels.Where(l => vm.LabelIds.Contains(l.Id)).ToListAsync();

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();
        var task = await _db.Tasks.Include(t => t.Labels).FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();

        await LoadLookups();
        return View(new TaskFormViewModel
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            Priority = task.Priority,
            DueDate = task.DueDate,
            ProjectId = task.ProjectId,
            AssigneeId = task.AssigneeId,
            LabelIds = task.Labels.Select(l => l.Id).ToList(),
            ExistingAttachmentUrl = task.AttachmentUrl
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TaskFormViewModel vm)
    {
        if (id != vm.Id) return NotFound();
        var task = await _db.Tasks.Include(t => t.Labels).FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();

        await ValidateReferences(vm);

        string? uploaded = null;
        if (ModelState.IsValid)
        {
            try { uploaded = await SaveAttachment(vm.Attachment); }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(vm.Attachment), ex.Message);
            }
        }

        if (!ModelState.IsValid)
        {
            vm.ExistingAttachmentUrl = task.AttachmentUrl;
            await LoadLookups();
            return View(vm);
        }

        var oldUrl = task.AttachmentUrl;
        Apply(task, vm);
        if (uploaded is not null) task.AttachmentUrl = uploaded;

        var labels = await _db.Labels.Where(l => vm.LabelIds.Contains(l.Id)).ToListAsync();
        task.Labels.Clear();
        task.Labels.AddRange(labels);

        await _db.SaveChangesAsync();
        if (uploaded is not null) _storage.Delete(oldUrl);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();
        var task = await _db.Tasks.Include(t => t.Project).FirstOrDefaultAsync(t => t.Id == id);
        return task is null ? NotFound() : View(task);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var task = await _db.Tasks.FindAsync(id);
        if (task is not null)
        {
            var url = task.AttachmentUrl;
            _db.Tasks.Remove(task);
            await _db.SaveChangesAsync();
            _storage.Delete(url);
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<string?> SaveAttachment(IFormFile? file) =>
        file is null || file.Length == 0 ? null : await _storage.SaveAsync(file);

    private async Task ValidateReferences(TaskFormViewModel vm)
    {
        if (vm.ProjectId > 0 && !await _db.Projects.AnyAsync(p => p.Id == vm.ProjectId))
            ModelState.AddModelError(nameof(vm.ProjectId), "Project not found");
        if (vm.AssigneeId.HasValue && !await _db.Users.AnyAsync(u => u.Id == vm.AssigneeId.Value))
            ModelState.AddModelError(nameof(vm.AssigneeId), "User not found");
    }

    private static void Apply(TaskItem t, TaskFormViewModel vm)
    {
        t.Title = vm.Title.Trim();
        t.Description = vm.Description;
        t.Status = vm.Status;
        t.Priority = vm.Priority;
        t.DueDate = vm.DueDate.HasValue
            ? DateTime.SpecifyKind(vm.DueDate.Value.Date, DateTimeKind.Utc)
            : null;
        t.ProjectId = vm.ProjectId;
        t.AssigneeId = vm.AssigneeId;
    }

    private async Task LoadLookups()
    {
        ViewBag.Projects = new SelectList(await _db.Projects.OrderBy(p => p.Name).ToListAsync(), "Id", "Name");
        ViewBag.Users = new SelectList(await _db.Users.OrderBy(u => u.Name).ToListAsync(), "Id", "Name");
        ViewBag.Labels = new MultiSelectList(await _db.Labels.OrderBy(l => l.Name).ToListAsync(), "Id", "Name");
    }
}
