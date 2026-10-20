using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Data;
using TaskFlow.Web.Models;

namespace TaskFlow.Web.Controllers;

public class UsersController : Controller
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index() =>
        View(await _db.Users.OrderBy(u => u.Name).ToListAsync());

    public IActionResult Create() => View(new AppUser());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Email,AvatarUrl")] AppUser user)
    {
        if (await _db.Users.AnyAsync(u => u.Email == user.Email))
            ModelState.AddModelError(nameof(user.Email), "Такий email уже існує");
        if (!ModelState.IsValid) return View(user);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();
        var user = await _db.Users.FindAsync(id);
        return user is null ? NotFound() : View(user);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Email,AvatarUrl")] AppUser input)
    {
        if (id != input.Id) return NotFound();
        if (await _db.Users.AnyAsync(u => u.Email == input.Email && u.Id != id))
            ModelState.AddModelError(nameof(input.Email), "The email is already in use");
        if (!ModelState.IsValid) return View(input);

        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();
        user.Name = input.Name;
        user.Email = input.Email;
        user.AvatarUrl = input.AvatarUrl;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();
        var user = await _db.Users.FindAsync(id);
        return user is null ? NotFound() : View(user);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is not null)
        {
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
