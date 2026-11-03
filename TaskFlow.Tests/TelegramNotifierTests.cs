using System.Net;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Web.Controllers;
using TaskFlow.Web.Models;
using TaskFlow.Web.Services;
using TaskFlow.Web.ViewModels;

namespace TaskFlow.Tests;

//підміна INotifier: запам'ятовує повідомлення, які мали бути надіслані
public class FakeNotifier : INotifier
{
    public List<string> Messages { get; } = new();
    public Task NotifyAsync(string text)
    {
        Messages.Add(text);
        return Task.CompletedTask;
    }
}

//підміна HTTP-обробника: нічого не відправляє в інтернет, а зберігає запит
public class FakeHttpHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = new();
    public string? LastBody { get; private set; }
    public bool Throw { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (Throw) throw new HttpRequestException("network down");
        Requests.Add(request);
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}

public class TelegramNotifierTests
{
    private static TelegramNotifier Make(FakeHttpHandler handler, string? token = "TOKEN", string? chat = "42") =>
        new(new HttpClient(handler), new TelegramOptions { BotToken = token, ChatId = chat });

    //перевірка, що повідомлення йде на правильну адресу Telegram і містить chat id та текст
    [Fact]
    public async Task Notify_SendsMessageToTelegramApi()
    {
        var handler = new FakeHttpHandler();

        await Make(handler).NotifyAsync("Привіт");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.telegram.org/botTOKEN/sendMessage", request.RequestUri!.ToString());
        Assert.Contains("\"chat_id\":\"42\"", handler.LastBody);
        Assert.Contains("\"text\":", handler.LastBody);
    }

    //перевірка, що без токена запит взагалі не відправляється
    [Fact]
    public async Task Notify_WithoutToken_SendsNothing()
    {
        var handler = new FakeHttpHandler();

        await Make(handler, token: "").NotifyAsync("x");

        Assert.Empty(handler.Requests);
    }

    //перевірка, що помилка мережі не ламає програму
    [Fact]
    public async Task Notify_NetworkError_DoesNotThrow()
    {
        var handler = new FakeHttpHandler { Throw = true };

        var ex = await Record.ExceptionAsync(() => Make(handler).NotifyAsync("x"));

        Assert.Null(ex);
    }

    //перевірка, що при створенні задачі контролер надсилає сповіщення
    [Fact]
    public async Task CreateTask_SendsNotification()
    {
        using var db = new TestDb();
        var project = new Project { Name = "P" };
        db.Context.Projects.Add(project);
        await db.Context.SaveChangesAsync();
        var notifier = new FakeNotifier();
        var controller = new TasksController(db.Context, new FakeFileStorage(), notifier);

        await controller.Create(new TaskFormViewModel { Title = "Fix bug", ProjectId = project.Id });

        var message = Assert.Single(notifier.Messages);
        Assert.Contains("Fix bug", message);
    }

    //перевірка, що при помилці валідації сповіщення не надсилається
    [Fact]
    public async Task CreateTask_Invalid_DoesNotNotify()
    {
        using var db = new TestDb();
        var notifier = new FakeNotifier();
        var controller = new TasksController(db.Context, new FakeFileStorage(), notifier);

        var result = await controller.Create(new TaskFormViewModel { Title = "Orphan", ProjectId = 999 });

        Assert.IsType<ViewResult>(result);
        Assert.Empty(notifier.Messages);
    }
}