using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Data;
using TaskFlow.Web.Models;
using TaskFlow.Web.Services;

namespace TaskFlow.Web.Api;

[ApiController]
[Route("api/tasks")]
public class TasksApiController : ControllerBase
{
    private static readonly Expression<Func<TaskItem, TaskDto>> ToDto = t => new TaskDto(
        t.Id, t.Title, t.Description, t.Status, t.Priority, t.DueDate, t.AttachmentUrl,
        t.ProjectId, t.AssigneeId, t.Labels.Select(l => l.Id).ToList());

    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;

    public TasksApiController(AppDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<TaskDto>>> List(
        int? projectId, ItemStatus? status, string? q, int skip = 0, int limit = Paging.DefaultLimit)
    {
        if (Paging.Validate(skip, limit) is { } error) return Problem(detail: error, statusCode: 400);

        var query = _db.Tasks.AsQueryable();
        if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId.Value);
        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(t => t.Title.ToLower().Contains(term));
        }

        var projected = query.OrderBy(t => t.Id).Select(ToDto);
        return await Paging.ToPagedAsync(projected, skip, limit, Request);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskDto>> Get(int id)
    {
        var dto = await _db.Tasks.Where(t => t.Id == id).Select(ToDto).FirstOrDefaultAsync();
        return dto is null ? NotFound() : dto;
    }

    [HttpPost]
    public async Task<ActionResult<TaskDto>> Create(TaskInput input)
    {
        await ValidateReferences(input);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var task = new TaskItem();
        Apply(task, input);
        task.Labels = await _db.Labels.Where(l => input.LabelIds.Contains(l.Id)).ToListAsync();
        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();

        var dto = await _db.Tasks.Where(t => t.Id == task.Id).Select(ToDto).SingleAsync();
        return CreatedAtAction(nameof(Get), new { id = task.Id }, dto);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TaskDto>> Update(int id, TaskInput input)
    {
        var task = await _db.Tasks.Include(t => t.Labels).FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return NotFound();

        await ValidateReferences(input);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        Apply(task, input);
        var labels = await _db.Labels.Where(l => input.LabelIds.Contains(l.Id)).ToListAsync();
        task.Labels.Clear();
        task.Labels.AddRange(labels);
        await _db.SaveChangesAsync();

        return await _db.Tasks.Where(t => t.Id == id).Select(ToDto).SingleAsync();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var task = await _db.Tasks.FindAsync(id);
        if (task is null) return NotFound();
        var url = task.AttachmentUrl;
        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync();
        _storage.Delete(url);
        return NoContent();
    }

    [HttpPost("{id:int}/attachment")]
    public async Task<ActionResult<TaskDto>> UploadAttachment(int id, IFormFile file)
    {
        var task = await _db.Tasks.FindAsync(id);
        if (task is null) return NotFound();
        if (file.Length == 0) return Problem(detail: "Порожній файл", statusCode: 400);

        string url;
        try { url = await _storage.SaveAsync(file); }
        catch (InvalidOperationException ex) { return Problem(detail: ex.Message, statusCode: 400); }

        var old = task.AttachmentUrl;
        task.AttachmentUrl = url;
        await _db.SaveChangesAsync();
        _storage.Delete(old);
        return await _db.Tasks.Where(t => t.Id == id).Select(ToDto).SingleAsync();
    }

    [HttpGet("{id:int}/comments")]
    public async Task<ActionResult<List<CommentDto>>> Comments(int id)
    {
        if (!await _db.Tasks.AnyAsync(t => t.Id == id)) return NotFound();
        return await _db.Comments.Where(c => c.TaskItemId == id).OrderBy(c => c.CreatedAt)
            .Select(c => new CommentDto(c.Id, c.Text, c.CreatedAt, c.AuthorId, c.Author!.Name))
            .ToListAsync();
    }

    [HttpPost("{id:int}/comments")]
    public async Task<ActionResult<CommentDto>> AddComment(int id, CommentInput input)
    {
        if (!await _db.Tasks.AnyAsync(t => t.Id == id)) return NotFound();
        var author = await _db.Users.FindAsync(input.AuthorId);
        if (author is null)
        {
            ModelState.AddModelError(nameof(input.AuthorId), "Користувача не знайдено");
            return ValidationProblem(ModelState);
        }
        var comment = new Comment { TaskItemId = id, AuthorId = author.Id, Text = input.Text.Trim(), CreatedAt = DateTime.UtcNow };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();
        var dto = new CommentDto(comment.Id, comment.Text, comment.CreatedAt, author.Id, author.Name);
        return Created($"/api/tasks/{id}/comments/{comment.Id}", dto);
    }

    [HttpDelete("{id:int}/comments/{commentId:int}")]
    public async Task<IActionResult> DeleteComment(int id, int commentId)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId && c.TaskItemId == id);
        if (comment is null) return NotFound();
        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ValidateReferences(TaskInput input)
    {
        if (input.ProjectId > 0 && !await _db.Projects.AnyAsync(p => p.Id == input.ProjectId))
            ModelState.AddModelError(nameof(input.ProjectId), "Проєкт не знайдено");
        if (input.AssigneeId.HasValue && !await _db.Users.AnyAsync(u => u.Id == input.AssigneeId.Value))
            ModelState.AddModelError(nameof(input.AssigneeId), "Користувача не знайдено");
    }

    private static void Apply(TaskItem t, TaskInput input)
    {
        t.Title = input.Title.Trim();
        t.Description = input.Description;
        t.Status = input.Status;
        t.Priority = input.Priority;
        t.DueDate = input.DueDate.HasValue
            ? DateTime.SpecifyKind(input.DueDate.Value.Date, DateTimeKind.Utc)
            : null;
        t.ProjectId = input.ProjectId;
        t.AssigneeId = input.AssigneeId;
    }
}
