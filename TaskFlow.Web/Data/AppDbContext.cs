using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Models;

namespace TaskFlow.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Label> Labels => Set<Label>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<TaskItem>()
            .HasOne(t => t.Assignee).WithMany()
            .HasForeignKey(t => t.AssigneeId)
            .OnDelete(DeleteBehavior.SetNull);

        b.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
    }
}
