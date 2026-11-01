using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Data;
using TaskFlow.Web.Models;

namespace TaskFlow.Web.Services;

public record StatItem(string Label, int Value);

public record StatsResult(int Total, List<StatItem> ByStatus, List<StatItem> ByProject, List<StatItem> ByAssignee);

public class StatsService
{
    public const string Unassigned = "Без виконавця";
    private readonly AppDbContext _db;

    public StatsService(AppDbContext db) => _db = db;

    public async Task<StatsResult> GetAsync()
    {
        // 1) за статусами — завжди всі 3 статуси, навіть якщо завдань 0
        var statusCounts = await _db.Tasks.GroupBy(t => t.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var byStatus = Enum.GetValues<ItemStatus>()
            .Select(s => new StatItem(DisplayNames.Status(s), statusCounts.FirstOrDefault(x => x.Key == s)?.Count ?? 0))
            .ToList();

        // 2) за проєктами — топ-10 за кількістю завдань
        var byProject = (await _db.Projects.Select(p => new { p.Name, Count = p.Tasks.Count }).ToListAsync())
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name)
            .Take(10).Select(x => new StatItem(x.Name, x.Count)).ToList();

        // 3) за виконавцями + окрема група «Без виконавця»
        var assigneeCounts = await _db.Tasks.GroupBy(t => t.AssigneeId)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var ids = assigneeCounts.Where(x => x.Key != null).Select(x => x.Key!.Value).ToList();
        var names = await _db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);
        var byAssignee = assigneeCounts
            .Select(x => new StatItem(x.Key is int id && names.TryGetValue(id, out var n) ? n : Unassigned, x.Count))
            .OrderByDescending(x => x.Value).ThenBy(x => x.Label)
            .ToList();

        return new StatsResult(statusCounts.Sum(x => x.Count), byStatus, byProject, byAssignee);
    }
}
