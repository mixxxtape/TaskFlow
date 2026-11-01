using System.Text.Json;
using TaskFlow.Web.Models;
using TaskFlow.Web.Services;

namespace TaskFlow.Web.ViewModels;

public class ProjectLandingViewModel
{
    public Project Project { get; init; } = null!;
    public List<TaskItem> Tasks { get; init; } = new();
    public List<AppUser> Team { get; init; } = new();
    public int Total { get; init; }
    public int[] StatusCounts { get; init; } = new int[3];
    public int CompletionPercent { get; init; }
    public string MetaDescription { get; init; } = "";
    public string CanonicalUrl { get; init; } = "";
    public string? ImageUrl { get; init; }
    public string JsonLd { get; init; } = "";

    public static ProjectLandingViewModel Create(Project project, string baseUrl)
    {
        baseUrl = baseUrl.TrimEnd('/');

        var tasks = project.Tasks.OrderBy(t => t.Status).ThenBy(t => t.Id).ToList();
        var counts = Enum.GetValues<ItemStatus>().Select(s => tasks.Count(t => t.Status == s)).ToArray();
        var done = counts[(int)ItemStatus.Done];
        var percent = tasks.Count == 0 ? 0 : (int)Math.Round(100.0 * done / tasks.Count, MidpointRounding.AwayFromZero);

        var team = tasks.Where(t => t.Assignee != null).Select(t => t.Assignee!)
            .DistinctBy(u => u.Id).OrderBy(u => u.Name).ToList();

        var description = SeoHelper.Description(project.Description,
            $"Проєкт «{project.Name}» у TaskFlow: {tasks.Count} завдань, виконано {percent}%.");
        var canonical = $"{baseUrl}/Projects/Details/{project.Id}";
        var image = string.IsNullOrEmpty(project.CoverImageUrl)
            ? null
            : project.CoverImageUrl.StartsWith('/') ? baseUrl + project.CoverImageUrl : project.CoverImageUrl;

        var ld = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebPage",
            ["name"] = project.Name,
            ["description"] = description,
            ["url"] = canonical,
            ["dateCreated"] = project.CreatedAt.ToString("yyyy-MM-dd")
        };
        if (image != null) ld["image"] = image;

        return new ProjectLandingViewModel
        {
            Project = project,
            Tasks = tasks,
            Team = team,
            Total = tasks.Count,
            StatusCounts = counts,
            CompletionPercent = percent,
            MetaDescription = description,
            CanonicalUrl = canonical,
            ImageUrl = image,
            JsonLd = JsonSerializer.Serialize(ld)
        };
    }
}
