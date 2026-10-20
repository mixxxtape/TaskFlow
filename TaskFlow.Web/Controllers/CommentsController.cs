using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Data;
using TaskFlow.Web.Models;

namespace TaskFlow.Web.Controllers;

public class CommentsController : Controller
{
    private readonly AppDbContext _db;
    public CommentsController(AppDbContext db) => _db = db;

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int taskItemId, int authorId, string? text)
    {
        if (!await _db.Tasks.AnyAsync(t => t.Id == taskItemId)) return NotFound();

        var valid = !string.IsNullOrWhiteSpace(text) && text.Length <= 2000
                    && await _db.Users.AnyAsync(u => u.Id == authorId);
        if (valid)
        {
            _db.Comments.Add(new Comment
            {
                TaskItemId = taskItemId,
                AuthorId = authorId,
                Text = text!.Trim(),
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Details", "Tasks", new { id = taskItemId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var comment = await _db.Comments.FindAsync(id);
        if (comment is null) return NotFound();
        var taskId = comment.TaskItemId;
        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync();
        return RedirectToAction("Details", "Tasks", new { id = taskId });
    }
}
