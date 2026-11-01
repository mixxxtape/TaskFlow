using Microsoft.AspNetCore.Mvc;
using TaskFlow.Web.Controllers;
using TaskFlow.Web.Models;
using TaskFlow.Web.Services;

namespace TaskFlow.Tests;

//F1: числа для діаграм
public class StatsTests
{
    //наповнення тестової бази проєктами, завданнями та виконавцями для перевірки статистики
    private static async Task SeedAsync(TestDb db)
    {
        var ann = new AppUser { Name = "Ann", Email = "ann@x.com" };
        var bob = new AppUser { Name = "Bob", Email = "bob@x.com" };
        var big = new Project { Name = "Big", Tasks =
        {
            new TaskItem { Title = "1", Status = ItemStatus.Done, Assignee = ann },
            new TaskItem { Title = "2", Status = ItemStatus.Done, Assignee = ann },
            new TaskItem { Title = "3", Status = ItemStatus.ToDo, Assignee = bob },
            new TaskItem { Title = "4", Status = ItemStatus.ToDo }
        }};
        var small = new Project { Name = "Small", Tasks = { new TaskItem { Title = "5", Status = ItemStatus.InProgress } } };
        var empty = new Project { Name = "Empty" };
        db.Context.AddRange(big, small, empty);
        await db.Context.SaveChangesAsync();
    }

    //перевірка, що порожня база повертає всі статуси з нульовими значеннями та не містить проєктів і виконавців
    [Fact]
    public async Task EmptyDatabase_ReturnsAllStatusesWithZero()
    {
        using var db = new TestDb();

        var result = await new StatsService(db.Context).GetAsync();

        Assert.Equal(0, result.Total);
        Assert.Equal(new[] { "До виконання", "У роботі", "Виконано" }, result.ByStatus.Select(s => s.Label));
        Assert.All(result.ByStatus, s => Assert.Equal(0, s.Value));
        Assert.Empty(result.ByProject);
        Assert.Empty(result.ByAssignee);
    }

    //перевірка правильного підрахунку завдань за кожним статусом
    [Fact]
    public async Task ByStatus_CountsEachStatus()
    {
        using var db = new TestDb();
        await SeedAsync(db);

        var result = await new StatsService(db.Context).GetAsync();

        Assert.Equal(5, result.Total);
        Assert.Equal(new[] { 2, 1, 2 }, result.ByStatus.Select(s => s.Value)); // ToDo, InProgress, Done
    }

    //перевірка сортування проєктів за кількістю завдань та включення проєктів без завдань
    [Fact]
    public async Task ByProject_IsSortedByCountDescending_AndIncludesEmptyProjects()
    {
        using var db = new TestDb();
        await SeedAsync(db);

        var result = await new StatsService(db.Context).GetAsync();

        Assert.Equal(new[] { ("Big", 4), ("Small", 1), ("Empty", 0) },
            result.ByProject.Select(p => (p.Label, p.Value)));
    }

    //перевірка, що статистика повертає не більше десяти проєктів
    [Fact]
    public async Task ByProject_ReturnsAtMostTen()
    {
        using var db = new TestDb();
        for (var i = 1; i <= 15; i++) db.Context.Projects.Add(new Project { Name = $"P{i:D2}" });
        await db.Context.SaveChangesAsync();

        var result = await new StatsService(db.Context).GetAsync();

        Assert.Equal(10, result.ByProject.Count);
    }

    //перевірка, що завдання без виконавця групуються окремо від завдань призначених користувачам
    [Fact]
    public async Task ByAssignee_GroupsUnassignedTasksSeparately()
    {
        using var db = new TestDb();
        await SeedAsync(db);

        var result = await new StatsService(db.Context).GetAsync();

        Assert.Equal(new[] { ("Без виконавця", 2), ("Ann", 2), ("Bob", 1) },
            result.ByAssignee.Select(a => (a.Label, a.Value)));
    }

    [Fact]
    public async Task Controller_Data_ReturnsJsonWithStats()
    {
        using var db = new TestDb();
        await SeedAsync(db);
        var controller = new StatsController(new StatsService(db.Context));

        var json = Assert.IsType<JsonResult>(await controller.Data());

        var stats = Assert.IsType<StatsResult>(json.Value);
        Assert.Equal(5, stats.Total);
    }

    //перевірка, що endpoint Data повертає JSON з результатами статистики
    [Fact]
    public void Controller_Index_ReturnsView() =>
        Assert.IsType<ViewResult>(new StatsController(null!).Index());
}
