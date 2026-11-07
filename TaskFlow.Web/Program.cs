using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Data;
using TaskFlow.Web.Services;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(o =>
    o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddWebEncoders(o =>
    o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.DocInclusionPredicate((_, api) => api.RelativePath?.StartsWith("api/") == true));

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IFileStorage>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    return new LocalFileStorage(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"));
});

builder.Services.AddScoped<StatsService>();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ApiCache>();

builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 1_000_000); 
builder.Services.AddSingleton<LiveState>();
builder.Services.AddSingleton<ILiveBroadcaster, LiveBroadcaster>();

builder.Services.AddScoped<UserRegistrar>();

var googleId = builder.Configuration["Authentication:Google:ClientId"];
var googleSecret = builder.Configuration["Authentication:Google:ClientSecret"];

builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogle(o =>
{
    o.ClientId = string.IsNullOrWhiteSpace(googleId) ? "not-configured" : googleId;
    o.ClientSecret = string.IsNullOrWhiteSpace(googleSecret) ? "not-configured" : googleSecret;

    o.Events.OnCreatingTicket = async ctx =>
    {
        var email = ctx.Principal?.FindFirst(ClaimTypes.Email)?.Value;
        if (email is null) return;

        var registrar = ctx.HttpContext.RequestServices.GetRequiredService<UserRegistrar>();
        await registrar.EnsureUserAsync(
            email,
            ctx.Principal!.FindFirst(ClaimTypes.Name)?.Value,
            ctx.Principal.FindFirst("urn:google:picture")?.Value);
    };
});

var telegram = builder.Configuration.GetSection("Telegram").Get<TelegramOptions>() ?? new TelegramOptions();
builder.Services.AddSingleton(telegram);
builder.Services.AddHttpClient<INotifier, TelegramNotifier>();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();

    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Projects}/{action=Index}/{id?}");

app.MapHub<LiveHub>("/hubs/live");

app.Run();

public partial class Program { }
