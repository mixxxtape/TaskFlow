using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Controllers;
using TaskFlow.Web.Models;
using TaskFlow.Web.ViewModels;

namespace TaskFlow.Tests;

public class TasksControllerTests
{
    private static async Task<Project> SeedProject(TestDb db)
    {
        var p = new Project { Name = "P" };
        db.Context.Projects.Add(p);
        await db.Context.SaveChangesAsync();
        return p;
    }

    [Fact]
    public async Task Create_ValidTask_IsSavedWithRelations()
    {
        using var db = new TestDb();
        var project = await SeedProject(db);
        var user = new AppUser { Name = "Ann", Email = "ann@x.com" };
        db.Context.Users.Add(user);
        await db.Context.SaveChangesAsync();
        var controller = new TasksController(db.Context, new FakeFileStorage());

        var result = await controller.Create(new TaskFormViewModel
        {
            Title = "Fix bug",
            ProjectId = project.Id,
            AssigneeId = user.Id,
            Status = ItemStatus.InProgress,
            DueDate = new DateTime(2026, 11, 1) // Kind = Unspecified, як у формі
        });

        Assert.IsType<RedirectToActionResult>(result);
        var task = await db.Context.Tasks.SingleAsync();
        Assert.Equal("Fix bug", task.Title);
        Assert.Equal(project.Id, task.ProjectId);
        Assert.Equal(user.Id, task.AssigneeId);
        Assert.Equal(ItemStatus.InProgress, task.Status);
        Assert.Equal(DateTimeKind.Utc, task.DueDate!.Value.Kind);
    }

    [Fact]
    public async Task Create_WithAttachment_StoresFileUrl()
    {
        using var db = new TestDb();
        var project = await SeedProject(db);
        var storage = new FakeFileStorage();
        var controller = new TasksController(db.Context, storage);

        await controller.Create(new TaskFormViewModel
        {
            Title = "With file",
            ProjectId = project.Id,
            Attachment = Files.Fake("spec.pdf")
        });

        var task = await db.Context.Tasks.SingleAsync();
        Assert.Equal("/uploads/fake-spec.pdf", task.AttachmentUrl);
        Assert.Single(storage.Saved);
    }

    [Fact]
    public async Task Create_WithLabels_LinksManyToMany()
    {
        using var db = new TestDb();
        var project = await SeedProject(db);
        var bug = new Label { Name = "bug" };
        var ui = new Label { Name = "ui" };
        db.Context.Labels.AddRange(bug, ui);
        await db.Context.SaveChangesAsync();
        var controller = new TasksController(db.Context, new FakeFileStorage());

        await controller.Create(new TaskFormViewModel
        {
            Title = "Labelled",
            ProjectId = project.Id,
            LabelIds = new List<int> { bug.Id }
        });

        var task = await db.Context.Tasks.Include(t => t.Labels).SingleAsync();
        Assert.Equal("bug", Assert.Single(task.Labels).Name);
    }

    [Fact]
    public async Task Create_UnknownProject_ReturnsViewAndDoesNotSave()
    {
        using var db = new TestDb();
        var controller = new TasksController(db.Context, new FakeFileStorage());

        var result = await controller.Create(new TaskFormViewModel { Title = "Orphan", ProjectId = 999 });

        Assert.IsType<ViewResult>(result);
        Assert.Empty(db.Context.Tasks);
    }

    [Fact]
    public async Task Create_EmptyTitle_ReturnsViewAndDoesNotSave()
    {
        using var db = new TestDb();
        var project = await SeedProject(db);
        var controller = new TasksController(db.Context, new FakeFileStorage());
        controller.ModelState.AddModelError("Title", "required");

        var result = await controller.Create(new TaskFormViewModel { Title = "", ProjectId = project.Id });

        Assert.IsType<ViewResult>(result);
        Assert.Empty(db.Context.Tasks);
    }

    [Fact]
    public async Task Edit_NewAttachment_ReplacesAndDeletesOldFile()
    {
        using var db = new TestDb();
        var project = await SeedProject(db);
        var task = new TaskItem { Title = "T", ProjectId = project.Id, AttachmentUrl = "/uploads/old.pdf" };
        db.Context.Tasks.Add(task);
        await db.Context.SaveChangesAsync();
        var storage = new FakeFileStorage();
        var controller = new TasksController(db.Context, storage);

        await controller.Edit(task.Id, new TaskFormViewModel
        {
            Id = task.Id,
            Title = "T2",
            ProjectId = project.Id,
            Attachment = Files.Fake("new.pdf")
        });

        db.Context.ChangeTracker.Clear();
        var reloaded = await db.Context.Tasks.SingleAsync();
        Assert.Equal("T2", reloaded.Title);
        Assert.Equal("/uploads/fake-new.pdf", reloaded.AttachmentUrl);
        Assert.Contains("/uploads/old.pdf", storage.Deleted);
    }

    [Fact]
    public async Task Edit_WithoutNewFile_KeepsExistingAttachment()
    {
        using var db = new TestDb();
        var project = await SeedProject(db);
        var task = new TaskItem { Title = "T", ProjectId = project.Id, AttachmentUrl = "/uploads/keep.pdf" };
        db.Context.Tasks.Add(task);
        await db.Context.SaveChangesAsync();
        var storage = new FakeFileStorage();
        var controller = new TasksController(db.Context, storage);

        await controller.Edit(task.Id, new TaskFormViewModel { Id = task.Id, Title = "Renamed", ProjectId = project.Id });

        db.Context.ChangeTracker.Clear();
        Assert.Equal("/uploads/keep.pdf", (await db.Context.Tasks.SingleAsync()).AttachmentUrl);
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task Edit_ReplacesLabelSet()
    {
        using var db = new TestDb();
        var project = await SeedProject(db);
        var a = new Label { Name = "a" };
        var b = new Label { Name = "b" };
        var task = new TaskItem { Title = "T", ProjectId = project.Id, Labels = { a } };
        db.Context.Tasks.Add(task);
        db.Context.Labels.Add(b);
        await db.Context.SaveChangesAsync();
        var controller = new TasksController(db.Context, new FakeFileStorage());

        await controller.Edit(task.Id, new TaskFormViewModel
        {
            Id = task.Id, Title = "T", ProjectId = project.Id, LabelIds = new List<int> { b.Id }
        });

        db.Context.ChangeTracker.Clear();
        var reloaded = await db.Context.Tasks.Include(t => t.Labels).SingleAsync();
        Assert.Equal("b", Assert.Single(reloaded.Labels).Name);
    }

    [Fact]
    public async Task Delete_RemovesTaskAndItsFile()
    {
        using var db = new TestDb();
        var project = await SeedProject(db);
        var task = new TaskItem { Title = "T", ProjectId = project.Id, AttachmentUrl = "/uploads/x.pdf" };
        db.Context.Tasks.Add(task);
        await db.Context.SaveChangesAsync();
        var storage = new FakeFileStorage();
        var controller = new TasksController(db.Context, storage);

        await controller.DeleteConfirmed(task.Id);

        Assert.Empty(db.Context.Tasks);
        Assert.Contains("/uploads/x.pdf", storage.Deleted);
    }

    [Fact]
    public async Task Index_FiltersByStatusAndSearch()
    {
        using var db = new TestDb();
        var project = await SeedProject(db);
        db.Context.Tasks.AddRange(
            new TaskItem { Title = "Write docs", ProjectId = project.Id, Status = ItemStatus.Done },
            new TaskItem { Title = "Write tests", ProjectId = project.Id, Status = ItemStatus.ToDo },
            new TaskItem { Title = "Deploy", ProjectId = project.Id, Status = ItemStatus.Done });
        await db.Context.SaveChangesAsync();
        var controller = new TasksController(db.Context, new FakeFileStorage());

        var byStatus = Assert.IsType<ViewResult>(await controller.Index(null, ItemStatus.Done, null));
        Assert.Equal(2, Assert.IsType<List<TaskItem>>(byStatus.Model).Count);

        var bySearch = Assert.IsType<ViewResult>(await controller.Index(null, null, "WRITE"));
        Assert.Equal(2, Assert.IsType<List<TaskItem>>(bySearch.Model).Count);

        var both = Assert.IsType<ViewResult>(await controller.Index(null, ItemStatus.Done, "write"));
        Assert.Single(Assert.IsType<List<TaskItem>>(both.Model));
    }
}
