using TaskFlow.Web.Api;
using TaskFlow.Web.Models;

namespace TaskFlow.Tests;

public class ProjectSearchTests
{
    private static async Task Seed(TestDb db, params string[] names)
    {
        foreach (var n in names) db.Context.Projects.Add(new Project { Name = n });
        await db.Context.SaveChangesAsync();
    }

    //перевірка, що пошук знаходить проєкти за частиною назви без урахування регістру
    [Fact]
    public async Task Search_FindsBySubstring_IgnoringCase()
    {
        using var db = new TestDb();
        await Seed(db, "Alpha site", "Beta app", "ALPHA mobile");
        var controller = new ProjectSearchApiController(db.Context);

        var result = await controller.Search("alp");

        Assert.Equal(2, result.Value!.Results.Count);
    }

    //перевірка, що запит коротший за 3 символи нічого не повертає
    [Fact]
    public async Task Search_ShortTerm_ReturnsEmpty()
    {
        using var db = new TestDb();
        await Seed(db, "Alpha", "Albatross");
        var controller = new ProjectSearchApiController(db.Context);

        var result = await controller.Search("al");

        Assert.Empty(result.Value!.Results);
    }

    //перевірка, що кількість результатів обмежена, навіть якщо збігів набагато більше
    [Fact]
    public async Task Search_LimitsNumberOfResults()
    {
        using var db = new TestDb();
        await Seed(db, Enumerable.Range(1, 30).Select(i => $"Project {i}").ToArray());
        var controller = new ProjectSearchApiController(db.Context);

        var result = await controller.Search("project");

        Assert.Equal(ProjectSearchApiController.MaxResults, result.Value!.Results.Count);
    }

    //перевірка, що коли збігів немає, повертається порожній список
    [Fact]
    public async Task Search_NoMatch_ReturnsEmpty()
    {
        using var db = new TestDb();
        await Seed(db, "Alpha");
        var controller = new ProjectSearchApiController(db.Context);

        var result = await controller.Search("zzzz");

        Assert.Empty(result.Value!.Results);
    }
}