using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Data;
using TaskFlow.Web.Models;

namespace TaskFlow.Web.Api;

[ApiController]
[Route("api/users")]
public class UsersApiController : ControllerBase
{
    private static readonly Expression<Func<AppUser, UserDto>> ToDto =
        u => new UserDto(u.Id, u.Name, u.Email, u.AvatarUrl);

    private readonly AppDbContext _db;
    public UsersApiController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserDto>>> List(int skip = 0, int limit = Paging.DefaultLimit)
    {
        if (Paging.Validate(skip, limit) is { } error) return Problem(detail: error, statusCode: 400);
        var query = _db.Users.OrderBy(u => u.Id).Select(ToDto);
        return await Paging.ToPagedAsync(query, skip, limit, Request);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> Get(int id)
    {
        var dto = await _db.Users.Where(u => u.Id == id).Select(ToDto).FirstOrDefaultAsync();
        return dto is null ? NotFound() : dto;
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(UserInput input)
    {
        if (await _db.Users.AnyAsync(u => u.Email == input.Email))
        {
            ModelState.AddModelError(nameof(input.Email), "Такий email уже існує");
            return ValidationProblem(ModelState);
        }
        var user = new AppUser { Name = input.Name.Trim(), Email = input.Email, AvatarUrl = input.AvatarUrl };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = user.Id }, new UserDto(user.Id, user.Name, user.Email, user.AvatarUrl));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserDto>> Update(int id, UserInput input)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();
        if (await _db.Users.AnyAsync(u => u.Email == input.Email && u.Id != id))
        {
            ModelState.AddModelError(nameof(input.Email), "Такий email уже існує");
            return ValidationProblem(ModelState);
        }
        user.Name = input.Name.Trim();
        user.Email = input.Email;
        user.AvatarUrl = input.AvatarUrl;
        await _db.SaveChangesAsync();
        return new UserDto(user.Id, user.Name, user.Email, user.AvatarUrl);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
