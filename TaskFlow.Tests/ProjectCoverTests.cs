using Microsoft.AspNetCore.Mvc;
using TaskFlow.Web.Controllers;
using TaskFlow.Web.Models;

namespace TaskFlow.Tests;

public class ProjectCoverTests
{
    //перевірка, що під час створення проєкту зображення зберігається, а контактний email записується в базу
    [Fact]
    public async Task Create_WithImage_SavesCoverUrlAndContact()
    {
        using var db = new TestDb();
        var controller = new ProjectsController(db.Context, new FakeFileStorage());

        var result = await controller.Create(new Project { Name = "P", ContactEmail = "team@x.com" }, Files.Fake("cover.png"));

        Assert.IsType<RedirectToActionResult>(result);
        var saved = db.Context.Projects.Single();
        Assert.Equal("/uploads/fake-cover.png", saved.CoverImageUrl);
        Assert.Equal("team@x.com", saved.ContactEmail);
    }

    //перевірка, що файл з недозволеним розширенням відхиляється, а проєкт і файл не зберігаються
    [Fact]
    public async Task Create_WithNonImage_ReturnsViewWithError_AndSavesNothing()
    {
        using var db = new TestDb();
        var storage = new FakeFileStorage();
        var controller = new ProjectsController(db.Context, storage);

        var result = await controller.Create(new Project { Name = "P" }, Files.Fake("doc.pdf"));

        Assert.IsType<ViewResult>(result);
        Assert.True(controller.ModelState.ContainsKey("cover"));
        Assert.Empty(db.Context.Projects);
        Assert.Empty(storage.Saved);
    }

    //перевірка, що проєкт можна створити без завантаження обкладинки
    [Fact]
    public async Task Create_WithoutCover_Works()
    {
        using var db = new TestDb();
        var controller = new ProjectsController(db.Context, new FakeFileStorage());

        await controller.Create(new Project { Name = "P" });

        Assert.Null(db.Context.Projects.Single().CoverImageUrl);
    }

    //перевірка, що нова обкладинка замінює стару, а попередній файл видаляється
    [Fact]
    public async Task Edit_NewCover_ReplacesAndDeletesOldFile()
    {
        using var db = new TestDb();
        var p = new Project { Name = "P", CoverImageUrl = "/uploads/old.png" };
        db.Context.Projects.Add(p);
        await db.Context.SaveChangesAsync();
        var storage = new FakeFileStorage();
        var controller = new ProjectsController(db.Context, storage);

        await controller.Edit(p.Id, new Project { Id = p.Id, Name = "P2", ContactEmail = "c@x.com" }, Files.Fake("new.jpg"));

        db.Context.ChangeTracker.Clear();
        var reloaded = db.Context.Projects.Single();
        Assert.Equal("/uploads/fake-new.jpg", reloaded.CoverImageUrl);
        Assert.Equal("c@x.com", reloaded.ContactEmail);
        Assert.Contains("/uploads/old.png", storage.Deleted);
    }

    //перевірка, що під час редагування без нового файлу стара обкладинка зберігається
    [Fact]
    public async Task Edit_WithoutNewCover_KeepsOldOne()
    {
        using var db = new TestDb();
        var p = new Project { Name = "P", CoverImageUrl = "/uploads/keep.png" };
        db.Context.Projects.Add(p);
        await db.Context.SaveChangesAsync();
        var storage = new FakeFileStorage();
        var controller = new ProjectsController(db.Context, storage);

        await controller.Edit(p.Id, new Project { Id = p.Id, Name = "Renamed" });

        db.Context.ChangeTracker.Clear();
        Assert.Equal("/uploads/keep.png", db.Context.Projects.Single().CoverImageUrl);
        Assert.Empty(storage.Deleted);
    }

    //перевірка, що під час видалення проєкту також видаляється файл його обкладинки
    [Fact]
    public async Task Delete_AlsoRemovesCoverFile()
    {
        using var db = new TestDb();
        var p = new Project { Name = "P", CoverImageUrl = "/uploads/cover.png" };
        db.Context.Projects.Add(p);
        await db.Context.SaveChangesAsync();
        var storage = new FakeFileStorage();
        var controller = new ProjectsController(db.Context, storage);

        await controller.DeleteConfirmed(p.Id);

        Assert.Contains("/uploads/cover.png", storage.Deleted);
    }
}
