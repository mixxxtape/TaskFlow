using Microsoft.AspNetCore.Mvc;
using TaskFlow.Web.Controllers;
using TaskFlow.Web.Models;

namespace TaskFlow.Tests;

public class ProjectsControllerTests
{
    [Fact]
    public async Task Create_ValidProject_IsSavedAndRedirects()
    {
        using var db = new TestDb();
        var controller = new ProjectsController(db.Context, new FakeFileStorage());

        var result = await controller.Create(new Project { Name = "Alpha", Description = "desc" });

        Assert.IsType<RedirectToActionResult>(result);
        var saved = Assert.Single(db.Context.Projects);
        Assert.Equal("Alpha", saved.Name);
    }

    [Fact]
    public async Task Create_InvalidModel_ReturnsViewAndDoesNotSave()
    {
        using var db = new TestDb();
        var controller = new ProjectsController(db.Context, new FakeFileStorage());
        controller.ModelState.AddModelError("Name", "required");

        var result = await controller.Create(new Project());

        Assert.IsType<ViewResult>(result);
        Assert.Empty(db.Context.Projects);
    }

    [Fact]
    public async Task Edit_UpdatesNameAndDescription_ButKeepsCreatedAt()
    {
        using var db = new TestDb();
        var created = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var project = new Project { Name = "Old", Description = "old", CreatedAt = created };
        db.Context.Projects.Add(project);
        await db.Context.SaveChangesAsync();
        var controller = new ProjectsController(db.Context, new FakeFileStorage());

        var result = await controller.Edit(project.Id, new Project { Id = project.Id, Name = "New", Description = "new" });

        Assert.IsType<RedirectToActionResult>(result);
        db.Context.ChangeTracker.Clear();
        var reloaded = await db.Context.Projects.FindAsync(project.Id);
        Assert.Equal("New", reloaded!.Name);
        Assert.Equal("new", reloaded.Description);
        Assert.Equal(created, reloaded.CreatedAt);
    }

    [Fact]
    public async Task Edit_IdMismatch_ReturnsNotFound()
    {
        using var db = new TestDb();
        var controller = new ProjectsController(db.Context, new FakeFileStorage());

        var result = await controller.Edit(1, new Project { Id = 2, Name = "x" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_RemovesProjectItsTasksAndTheirFiles()
    {
        using var db = new TestDb();
        var project = new Project
        {
            Name = "P",
            Tasks = { new TaskItem { Title = "t1", AttachmentUrl = "/uploads/a.pdf" }, new TaskItem { Title = "t2" } }
        };
        db.Context.Projects.Add(project);
        await db.Context.SaveChangesAsync();
        var storage = new FakeFileStorage();
        var controller = new ProjectsController(db.Context, storage);

        await controller.DeleteConfirmed(project.Id);

        Assert.Empty(db.Context.Projects);
        Assert.Empty(db.Context.Tasks);
        Assert.Contains("/uploads/a.pdf", storage.Deleted);
    }

    [Fact]
    public async Task Details_UnknownId_ReturnsNotFound()
    {
        using var db = new TestDb();
        var controller = new ProjectsController(db.Context, new FakeFileStorage());

        Assert.IsType<NotFoundResult>(await controller.Details(999));
    }
}
