using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Data;
using TaskFlow.Web.Models;

namespace TaskFlow.Web.Services;

public class UserRegistrar
{
    private readonly AppDbContext _db;
    public UserRegistrar(AppDbContext db) => _db = db;

    //знаходить користувача за email; якщо його немає, реєструє нового
    public async Task<AppUser> EnsureUserAsync(string email, string? name, string? avatarUrl)
    {
        email = email.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (user is null)
        {
            user = new AppUser
            {
                Email = email,
                Name = string.IsNullOrWhiteSpace(name) ? email : name.Trim(),
                AvatarUrl = avatarUrl
            };
            _db.Users.Add(user);
        }
        else if (user.AvatarUrl is null && avatarUrl is not null)
        {
            user.AvatarUrl = avatarUrl;
        }

        await _db.SaveChangesAsync();
        return user;
    }
}