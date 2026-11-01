using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Data;
using TaskFlow.Web.Services;

namespace TaskFlow.Web.Controllers;

public class SeoController : Controller
{
    private readonly AppDbContext _db;
    public SeoController(AppDbContext db) => _db = db;

    private string BaseUrl => $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap()
    {
        var projects = await _db.Projects.OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.CreatedAt }).ToListAsync();
        var xml = SitemapBuilder.Build(BaseUrl, projects.Select(p => (p.Id, p.CreatedAt)));
        return Content(xml, "application/xml", Encoding.UTF8);
    }

    [HttpGet("/robots.txt")]
    public IActionResult Robots() => Content(SeoHelper.Robots(BaseUrl), "text/plain", Encoding.UTF8);
}
