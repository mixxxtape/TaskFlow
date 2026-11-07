using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using TaskFlow.Web.Api;
using TaskFlow.Web.Controllers;
using TaskFlow.Web.Models;
using TaskFlow.Web.Services;
using TaskFlow.Web.ViewModels;

namespace TaskFlow.Tests;

//підміна ILiveBroadcaster: запам'ятовує повідомлення
public class FakeBroadcaster : ILiveBroadcaster
{
    public List<string> Messages { get; } = new();
    public Task PublishAsync(string message)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }
}

public class LiveStateTests
{
    //перевірка, що кожна нова подія отримує наступний номер і стає останньою
    [Fact]
    public void Next_IncrementsIdAndRemembersLast()
    {
        var state = new LiveState();

        var a = state.Next("one");
        var b = state.Next("two");

        Assert.Equal(1, a.Id);
        Assert.Equal(2, b.Id);
        Assert.Equal("two", state.Last!.Message);
    }
}

public class LiveBroadcasterTests
{
    //перевірка, що подія розсилається всім клієнтам SignalR під іменем LiveEvent
    [Fact]
    public async Task Publish_SendsEventToAllClients()
    {
        var proxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.All).Returns(proxy.Object);
        var hub = new Mock<IHubContext<LiveHub>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);
        var broadcaster = new LiveBroadcaster(hub.Object, new LiveState());

        await broadcaster.PublishAsync("hello");

        proxy.Verify(p => p.SendCoreAsync("LiveEvent",
            It.Is<object?[]>(a => a.Length == 1 && ((LiveEvent)a[0]!).Message == "hello"),
            default), Times.Once);
    }
}

public class LiveApiTests
{
    //перевірка, що коли подій ще не було, polling повертає 204 No Content
    [Fact]
    public void Latest_NoEvents_ReturnsNoContent()
    {
        var controller = new LiveApiController(new LiveState(), new FakeBroadcaster());

        Assert.IsType<NoContentResult>(controller.Latest(0).Result);
    }

    //перевірка, що polling віддає подію, якщо вона новіша за ту, яку клієнт уже бачив
    [Fact]
    public void Latest_NewerEvent_ReturnsIt()
    {
        var state = new LiveState();
        state.Next("hello");
        var controller = new LiveApiController(state, new FakeBroadcaster());

        var result = controller.Latest(0);

        Assert.Equal("hello", result.Value!.Message);
    }

    //перевірка, що вже побачену подію polling вдруге не віддає
    [Fact]
    public void Latest_AlreadySeen_ReturnsNoContent()
    {
        var state = new LiveState();
        var e = state.Next("hello");
        var controller = new LiveApiController(state, new FakeBroadcaster());

        Assert.IsType<NoContentResult>(controller.Latest(e.Id).Result);
    }

    //перевірка, що тестова кнопка публікує подію
    [Fact]
    public async Task Ping_PublishesEvent()
    {
        var broadcaster = new FakeBroadcaster();
        var controller = new LiveApiController(new LiveState(), broadcaster);

        var result = await controller.Ping();

        Assert.IsType<AcceptedResult>(result);
        Assert.Single(broadcaster.Messages);
    }
}

public class TaskCreatedEventTests
{
    //перевірка, що створення завдання публікує живу подію
    [Fact]
    public async Task CreateTask_PublishesLiveEvent()
    {
        using var db = new TestDb();
        var project = new Project { Name = "P" };
        db.Context.Projects.Add(project);
        await db.Context.SaveChangesAsync();
        var broadcaster = new FakeBroadcaster();
        var controller = new TasksController(db.Context, new FakeFileStorage(), live: broadcaster);

        await controller.Create(new TaskFormViewModel { Title = "Fix bug", ProjectId = project.Id });

        Assert.Contains("Fix bug", Assert.Single(broadcaster.Messages));
    }
}

public class LiveEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    public LiveEndpointsTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

    //перевірка, що хаб SignalR підключений і відповідає на запит узгодження транспорту
    [Fact]
    public async Task Hub_NegotiateEndpoint_Responds()
    {
        var response = await _client.PostAsync("/hubs/live/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    //перевірка, що сторінка порівняння відкривається
    [Fact]
    public async Task LivePage_Opens()
    {
        var response = await _client.GetAsync("/Live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}