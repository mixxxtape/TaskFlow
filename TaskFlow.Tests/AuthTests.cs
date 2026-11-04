using System.Net;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Web.Controllers;
using TaskFlow.Web.Models;
using TaskFlow.Web.Services;

namespace TaskFlow.Tests;

public class UserRegistrarTests
{
    //перевірка, що при першому вході створюється новий користувач (реєстрація)
    [Fact]
    public async Task EnsureUser_NewEmail_RegistersUser()
    {
        using var db = new TestDb();
        var registrar = new UserRegistrar(db.Context);

        await registrar.EnsureUserAsync("Ann@Gmail.com", "Ann", "http://pic");

        var user = await db.Context.Users.SingleAsync();
        Assert.Equal("ann@gmail.com", user.Email);
        Assert.Equal("Ann", user.Name);
        Assert.Equal("http://pic", user.AvatarUrl);
    }

    //перевірка, що при повторному вході дубліката не створюється (вхід без повторної реєстрації)
    [Fact]
    public async Task EnsureUser_ExistingEmail_DoesNotDuplicate()
    {
        using var db = new TestDb();
        db.Context.Users.Add(new AppUser { Name = "Ann", Email = "ann@gmail.com" });
        await db.Context.SaveChangesAsync();
        var registrar = new UserRegistrar(db.Context);

        await registrar.EnsureUserAsync("ANN@gmail.com", "Ann", null);

        Assert.Equal(1, await db.Context.Users.CountAsync());
    }

    //перевірка, що якщо Google не передав ім'я, замість нього використовується email
    [Fact]
    public async Task EnsureUser_NoName_UsesEmailAsName()
    {
        using var db = new TestDb();
        var registrar = new UserRegistrar(db.Context);

        var user = await registrar.EnsureUserAsync("bob@gmail.com", null, null);

        Assert.Equal("bob@gmail.com", user.Name);
    }

    //перевірка, що для існуючого користувача без аватарки вона дописується
    [Fact]
    public async Task EnsureUser_ExistingWithoutAvatar_FillsAvatar()
    {
        using var db = new TestDb();
        db.Context.Users.Add(new AppUser { Name = "Ann", Email = "ann@gmail.com" });
        await db.Context.SaveChangesAsync();
        var registrar = new UserRegistrar(db.Context);

        var user = await registrar.EnsureUserAsync("ann@gmail.com", "Ann", "http://pic");

        Assert.Equal("http://pic", user.AvatarUrl);
    }
}

public class AccountControllerTests
{
    //перевірка, що сторінка входу перенаправляє на Google
    [Fact]
    public void Login_ChallengesGoogle()
    {
        var result = new AccountController().Login();

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Contains(GoogleDefaults.AuthenticationScheme, challenge.AuthenticationSchemes);
    }

    //перевірка, що повернення на чужий сайт після входу заборонене
    [Theory]
    [InlineData("https://evil.com", "/")]
    [InlineData("//evil.com", "/")]
    [InlineData(null, "/")]
    [InlineData("/Tasks", "/Tasks")]
    public void SafeReturnUrl_AllowsOnlyLocalPaths(string? input, string expected)
    {
        Assert.Equal(expected, AccountController.SafeReturnUrl(input));
    }
}

public class AuthorizationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthorizationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    //перевірка, що незалогінений користувач не може відкрити створення завдання і його відправляє на Google
    [Fact]
    public async Task CreateTask_Anonymous_RedirectsToGoogle()
    {
        var response = await _client.GetAsync("/Tasks/Create");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("accounts.google.com", response.Headers.Location!.ToString());
    }

    //перевірка, що перегляд списку завдань залишається відкритим для всіх
    [Fact]
    public async Task TasksIndex_Anonymous_IsAvailable()
    {
        var response = await _client.GetAsync("/Tasks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}