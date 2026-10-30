using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Controllers;
using TaskFlow.Web.Models;

namespace TaskFlow.Tests;

public class CommentsAndUsersTests
{

    //створення тестового користувача і завдання, щоб використовувати їх в інших тестах
    private static async Task<(TaskItem task, AppUser user)> Seed(TestDb db)
    {
        var user = new AppUser { Name = "Ann", Email = "ann@x.com" };
        var task = new TaskItem { Title = "T", Project = new Project { Name = "P" } };
        db.Context.AddRange(user, task);
        await db.Context.SaveChangesAsync();
        return (task, user);
    }


    //перевірка, що комент створюється, а зайві пробіли на початку і в кінці прибираються
    [Fact]
    public async Task Comment_Create_AddsComment()
    {
        using var db = new TestDb();
        var (task, user) = await Seed(db);
        var controller = new CommentsController(db.Context);

        var result = await controller.Create(task.Id, user.Id, "  Looks good  ");

        Assert.IsType<RedirectToActionResult>(result);
        var comment = await db.Context.Comments.SingleAsync();
        Assert.Equal("Looks good", comment.Text);
        Assert.Equal(user.Id, comment.AuthorId);
    }


    //перевірка, що порожній комент не додається в базу даних
    [Fact]
    public async Task Comment_Create_EmptyText_IsIgnored()
    {
        using var db = new TestDb();
        var (task, user) = await Seed(db);
        var controller = new CommentsController(db.Context);

        await controller.Create(task.Id, user.Id, "   ");

        Assert.Empty(db.Context.Comments);
    }


    //перевірка, що при спробі додати коментар до неіснуючого завдання повертається NotFound
    [Fact]
    public async Task Comment_Create_UnknownTask_ReturnsNotFound()
    {
        using var db = new TestDb();
        var controller = new CommentsController(db.Context);

        Assert.IsType<NotFoundResult>(await controller.Create(999, 1, "x"));
    }

    //перевірка, що після видалення коментар зникає з бази даних
    [Fact]
    public async Task Comment_Delete_RemovesComment()
    {
        using var db = new TestDb();
        var (task, user) = await Seed(db);
        var c = new Comment { TaskItemId = task.Id, AuthorId = user.Id, Text = "x" };
        db.Context.Comments.Add(c);
        await db.Context.SaveChangesAsync();
        var controller = new CommentsController(db.Context);

        await controller.Delete(c.Id);

        Assert.Empty(db.Context.Comments);
    }


    //перевірка, що користувача з email, який вже є в базі, не можна створити повторно
    [Fact]
    public async Task User_Create_DuplicateEmail_IsRejected()
    {
        using var db = new TestDb();
        db.Context.Users.Add(new AppUser { Name = "A", Email = "dup@x.com" });
        await db.Context.SaveChangesAsync();
        var controller = new UsersController(db.Context);

        var result = await controller.Create(new AppUser { Name = "B", Email = "dup@x.com" });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(1, await db.Context.Users.CountAsync());
    }


    //перевірка, що після видалення користувача його завдання залишаються, але призначений виконавець у них очищується
    [Fact]
    public async Task User_Delete_KeepsTasksButClearsAssignee()
    {
        using var db = new TestDb();
        var (task, user) = await Seed(db);
        task.AssigneeId = user.Id;
        await db.Context.SaveChangesAsync();
        var controller = new UsersController(db.Context);

        await controller.DeleteConfirmed(user.Id);

        db.Context.ChangeTracker.Clear();
        var reloaded = await db.Context.Tasks.SingleAsync();
        Assert.Null(reloaded.AssigneeId);
        Assert.Empty(db.Context.Users);
    }


    //перевірка, що при видаленні мітки саме завдання не видаляється, а зв'язок із цією міткою зникає
    [Fact]
    public async Task Label_Delete_RemovesLabelButNotTask()
    {
        using var db = new TestDb();
        var label = new Label { Name = "bug" };
        var task = new TaskItem { Title = "T", Project = new Project { Name = "P" }, Labels = { label } };
        db.Context.Tasks.Add(task);
        await db.Context.SaveChangesAsync();
        var controller = new LabelsController(db.Context);

        await controller.DeleteConfirmed(label.Id);

        db.Context.ChangeTracker.Clear();
        Assert.Empty(db.Context.Labels);
        Assert.Empty((await db.Context.Tasks.Include(t => t.Labels).SingleAsync()).Labels);
    }
}
