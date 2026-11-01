using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Web.Controllers;
using TaskFlow.Web.Models;
using TaskFlow.Web.ViewModels;

namespace TaskFlow.Tests;

public class ProjectLandingViewModelTests
{
    //створення тестового проєкту з учасниками та завданнями для перевірки ViewModel
    private static Project Sample()
    {
        var ann = new AppUser { Id = 1, Name = "Ann", Email = "a@x.com" };
        var bob = new AppUser { Id = 2, Name = "Bob", Email = "b@x.com" };
        return new Project
        {
            Id = 5,
            Name = "Alpha",
            Description = "Alpha description",
            CreatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            Tasks =
            {
                new TaskItem { Id = 3, Title = "c", Status = ItemStatus.Done, Assignee = ann },
                new TaskItem { Id = 1, Title = "a", Status = ItemStatus.ToDo, Assignee = bob },
                new TaskItem { Id = 2, Title = "b", Status = ItemStatus.ToDo, Assignee = ann },
                new TaskItem { Id = 4, Title = "d", Status = ItemStatus.InProgress }
            }
        };
    }

    //перевірка загальної кількості завдань та порядку підрахунку за статусами
    [Fact]
    public void Create_CountsTasksPerStatus_InFixedOrder()
    {
        var vm = ProjectLandingViewModel.Create(Sample(), "https://example.com");

        Assert.Equal(4, vm.Total);
        Assert.Equal(new[] { 2, 1, 1 }, vm.StatusCounts); // ToDo, InProgress, Done
    }

    //перевірка правильного округлення відсотка виконання для різної кількості виконаних завдань
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]
    [InlineData(3, 3, 100)]
    public void Create_CompletionPercent_IsRounded(int done, int total, int expected)
    {
        var project = new Project { Id = 1, Name = "P" };
        for (var i = 0; i < total; i++)
            project.Tasks.Add(new TaskItem { Id = i + 1, Title = "t", Status = i < done ? ItemStatus.Done : ItemStatus.ToDo });

        Assert.Equal(expected, ProjectLandingViewModel.Create(project, "https://x.com").CompletionPercent);
    }

    //перевірка, що учасники команди не дублюються та сортуються за іменем
    [Fact]
    public void Create_Team_ContainsEachAssigneeOnce_SortedByName()
    {
        var vm = ProjectLandingViewModel.Create(Sample(), "https://example.com");
        Assert.Equal(new[] { "Ann", "Bob" }, vm.Team.Select(u => u.Name));
    }

    //перевірка сортування завдань за статусом, а потім за ідентифікатором
    [Fact]
    public void Create_Tasks_AreSortedByStatusThenId()
    {
        var vm = ProjectLandingViewModel.Create(Sample(), "https://example.com");
        Assert.Equal(new[] { 1, 2, 4, 3 }, vm.Tasks.Select(t => t.Id));
    }

    //перевірка формування canonical URL без подвійного слеша в адресі
    [Fact]
    public void Create_CanonicalUrl_UsesBaseUrlWithoutDoubleSlash()
    {
        var vm = ProjectLandingViewModel.Create(Sample(), "https://example.com/");
        Assert.Equal("https://example.com/Projects/Details/5", vm.CanonicalUrl);
    }

    //перевірка створення запасного meta description, якщо опис проєкту відсутній
    [Fact]
    public void Create_EmptyDescription_UsesGeneratedFallback()
    {
        var project = Sample();
        project.Description = null;

        var vm = ProjectLandingViewModel.Create(project, "https://example.com");

        Assert.Contains("Alpha", vm.MetaDescription);
        Assert.Contains("4 завдань", vm.MetaDescription);
    }

    //перевірка перетворення відносного URL обкладинки на абсолютний та обробки відсутнього зображення
    [Fact]
    public void Create_RelativeCover_BecomesAbsoluteUrl_AndNoCoverMeansNull()
    {
        var project = Sample();
        project.CoverImageUrl = "/uploads/a.png";
        Assert.Equal("https://example.com/uploads/a.png", ProjectLandingViewModel.Create(project, "https://example.com").ImageUrl);

        project.CoverImageUrl = null;
        Assert.Null(ProjectLandingViewModel.Create(project, "https://example.com").ImageUrl);
    }

    //перевірка коректності JSON-LD та наявності основних полів Schema.org
    [Fact]
    public void Create_JsonLd_IsValidJson_WithSchemaOrgFields()
    {
        var project = Sample();
        project.CoverImageUrl = "/uploads/a.png";

        var vm = ProjectLandingViewModel.Create(project, "https://example.com");
        using var doc = JsonDocument.Parse(vm.JsonLd);

        Assert.Equal("https://schema.org", doc.RootElement.GetProperty("@context").GetString());
        Assert.Equal("WebPage", doc.RootElement.GetProperty("@type").GetString());
        Assert.Equal("Alpha", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("https://example.com/Projects/Details/5", doc.RootElement.GetProperty("url").GetString());
        Assert.Equal("https://example.com/uploads/a.png", doc.RootElement.GetProperty("image").GetString());
    }

    //перевірка, що назва проєкту не дозволяє вставити сторонній HTML або JavaScript у JSON-LD
    [Fact]
    public void Create_JsonLd_CannotBreakOutOfScriptTag()
    {
        var project = Sample();
        project.Name = "</script><script>alert(1)</script>";

        var vm = ProjectLandingViewModel.Create(project, "https://example.com");

        Assert.DoesNotContain("</script>", vm.JsonLd, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<", vm.JsonLd);
    }

    //перевірка, що контролер Details формує ViewModel з адресою поточного HTTP-запиту
    [Fact]
    public async Task Controller_Details_BuildsViewModel_FromRequestHost()
    {
        using var db = new TestDb();
        db.Context.Projects.Add(Sample());
        await db.Context.SaveChangesAsync();
        var id = db.Context.Projects.Single().Id;
        var controller = new ProjectsController(db.Context, new FakeFileStorage())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Scheme = "https";
        controller.Request.Host = new HostString("example.com");

        var view = Assert.IsType<ViewResult>(await controller.Details(id));

        var vm = Assert.IsType<ProjectLandingViewModel>(view.Model);
        Assert.Equal($"https://example.com/Projects/Details/{id}", vm.CanonicalUrl);
        Assert.Equal(4, vm.Total);
        Assert.Equal(2, vm.Team.Count);
    }
}
