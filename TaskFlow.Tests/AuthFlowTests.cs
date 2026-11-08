using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TaskFlow.Tests;

//тестова схема входу: користувач вважається залогіненим, якщо в запиті є заголовок X-Test-User
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
	public const string Scheme = "Test";

	public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
		ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder) { }

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		if (!Request.Headers.TryGetValue("X-Test-User", out var name))
			return Task.FromResult(AuthenticateResult.NoResult());

		var identity = new ClaimsIdentity(new[]
		{
			new Claim(ClaimTypes.Name, name.ToString()),
			new Claim(ClaimTypes.Email, "tester@example.com")
		}, Scheme);
		var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme);
		return Task.FromResult(AuthenticateResult.Success(ticket));
	}
}

public class AuthFlowTests
{
	private static HttpClient CreateClient(string? loggedInAs = null)
	{
		var factory = new CustomWebApplicationFactory().WithWebHostBuilder(b =>
			b.ConfigureTestServices(services =>
				services.AddAuthentication(o => o.DefaultAuthenticateScheme = TestAuthHandler.Scheme)
						.AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { })));

		var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		if (loggedInAs is not null) client.DefaultRequestHeaders.Add("X-Test-User", loggedInAs);
		return client;
	}

	//перевірка, що незалогінений користувач при відкритті форми створення завдання потрапляє на Google
	[Fact]
	public async Task Anonymous_CreateForm_RedirectsToGoogle()
	{
		var response = await CreateClient().GetAsync("/Tasks/Create");

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Contains("accounts.google.com", response.Headers.Location!.ToString());
	}

	//перевірка, що незалогінений користувач не може відкрити редагування завдання
	[Fact]
	public async Task Anonymous_EditForm_RedirectsToGoogle()
	{
		var response = await CreateClient().GetAsync("/Tasks/Edit/1");

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
	}

	//перевірка, що незалогінений користувач не може надіслати форму створення завдання
	[Fact]
	public async Task Anonymous_CreatePost_RedirectsToGoogle()
	{
		var response = await CreateClient().PostAsync("/Tasks/Create", new FormUrlEncodedContent(
			new Dictionary<string, string> { ["Title"] = "Hack", ["ProjectId"] = "1" }));

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Contains("accounts.google.com", response.Headers.Location!.ToString());
	}

	//перевірка, що залогінений користувач бачить форму створення завдання
	[Fact]
	public async Task LoggedIn_CreateForm_IsAvailable()
	{
		var response = await CreateClient("Ann").GetAsync("/Tasks/Create");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	//перевірка, що перегляд списку завдань відкритий і без входу
	[Fact]
	public async Task Anonymous_TasksList_IsPublic()
	{
		var response = await CreateClient().GetAsync("/Tasks");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	//перевірка, що перегляд окремого завдання не вимагає входу (відповідь 404, але не перенаправлення на вхід)
	[Fact]
	public async Task Anonymous_TaskDetails_DoesNotRequireLogin()
	{
		var response = await CreateClient().GetAsync("/Tasks/Details/999");

		Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
	}

	//перевірка, що REST API залишається відкритим (від нього залежать Postman-тести з QA1)
	[Fact]
	public async Task Anonymous_Api_IsPublic()
	{
		var response = await CreateClient().GetAsync("/api/tasks");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	//перевірка, що сторінка входу веде на Google зі зверненням саме до нашого застосунку
	[Fact]
	public async Task Login_RedirectsToGoogleWithClientAndCallback()
	{
		var response = await CreateClient().GetAsync("/Account/Login");
		var location = response.Headers.Location!.ToString();

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Contains("accounts.google.com", location);
		Assert.Contains("client_id=", location);
		Assert.Contains("signin-google", location);
	}

	//перевірка, що вихід можливий тільки методом POST (GET не дозволений)
	[Fact]
	public async Task Logout_Get_IsNotAllowed()
	{
		var response = await CreateClient("Ann").GetAsync("/Account/Logout");

		Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
	}

	//перевірка, що без входу в меню є кнопка входу, а ім'я не показується
	[Fact]
	public async Task Anonymous_Page_ShowsLoginLink()
	{
		var html = await CreateClient().GetStringAsync("/Projects");

		Assert.Contains("Увійти через Google", html);
		Assert.DoesNotContain("Вийти", html);
	}

	//перевірка, що після входу в меню видно ім'я користувача і кнопку виходу
	[Fact]
	public async Task LoggedIn_Page_ShowsNameAndLogout()
	{
		var html = await CreateClient("Ann").GetStringAsync("/Projects");

		Assert.Contains("Ann", html);
		Assert.Contains("Вийти", html);
		Assert.DoesNotContain("Увійти через Google", html);
	}
}