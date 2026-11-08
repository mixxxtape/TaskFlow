using System.Net;
using Microsoft.AspNetCore.SignalR;
using Moq;
using TaskFlow.Web.Services;

namespace TaskFlow.Tests;

public class StrokeTests
{
    //перевірка, що правильний штрих залишається без змін
    [Fact]
    public void Clean_ValidStroke_IsKept()
    {
        var s = Stroke.Clean(new Stroke(10, 20, 30, 40, "#ff0000", 5));

        Assert.Equal(new Stroke(10, 20, 30, 40, "#ff0000", 5), s);
    }

    //перевірка, що координати поза полотном і занадто велика товщина обмежуються
    [Fact]
    public void Clean_OutOfRangeValues_AreClamped()
    {
        var s = Stroke.Clean(new Stroke(-50, 99999, double.NaN, 10, "#00ff00", 500));

        Assert.Equal(0, s.X0);
        Assert.Equal(2000, s.Y0);
        Assert.Equal(0, s.X1);
        Assert.Equal(30, s.Size);
    }

    //перевірка, що підозрілий "колір" замінюється на чорний (захист від вставки сторонніх значень)
    [Theory]
    [InlineData("red")]
    [InlineData("url(javascript:alert(1))")]
    [InlineData(null)]
    public void Clean_InvalidColor_BecomesBlack(string? color)
    {
        var s = Stroke.Clean(new Stroke(0, 0, 1, 1, color!, 3));

        Assert.Equal("#000000", s.Color);
    }
}

public class BoardStateTests
{
    private static Stroke S(int n) => new(n, n, n, n, "#000000", 1);

    //перевірка, що штрихи зберігаються в порядку додавання
    [Fact]
    public void Add_KeepsStrokesInOrder()
    {
        var state = new BoardState();
        state.Add(S(1));
        state.Add(S(2));

        var snapshot = state.Snapshot();

        Assert.Equal(new[] { 1.0, 2.0 }, snapshot.Select(x => x.X0));
    }

    //перевірка, що очищення дошки видаляє всю історію
    [Fact]
    public void Clear_RemovesAllStrokes()
    {
        var state = new BoardState();
        state.Add(S(1));

        state.Clear();

        Assert.Empty(state.Snapshot());
    }

    //перевірка, що історія не росте безмежно: при переповненні найстаріші штрихи викидаються
    [Fact]
    public void Add_OverLimit_DropsOldest()
    {
        var state = new BoardState();
        for (var i = 0; i <= BoardState.MaxStrokes; i++) state.Add(S(i));

        var snapshot = state.Snapshot();

        Assert.True(snapshot.Count <= BoardState.MaxStrokes);
        Assert.Equal(BoardState.MaxStrokes, snapshot.Last().X0);
    }

    //перевірка, що лічильник людей онлайн збільшується і зменшується
    [Fact]
    public void Users_CountJoinsAndLeaves()
    {
        var state = new BoardState();

        Assert.Equal(1, state.UserJoined());
        Assert.Equal(2, state.UserJoined());
        Assert.Equal(1, state.UserLeft());
    }
}

public class BoardHubTests
{
    //перевірка, що намальований штрих зберігається в історії і пересилається іншим
    [Fact]
    public async Task Draw_StoresStrokeAndForwardsToOthers()
    {
        var state = new BoardState();
        var others = new Mock<IClientProxy>();
        var clients = new Mock<IHubCallerClients>();
        clients.Setup(c => c.Others).Returns(others.Object);
        var hub = new BoardHub(state) { Clients = clients.Object };

        await hub.Draw(new Stroke(1, 2, 3, 4, "#ff0000", 5));

        Assert.Single(state.Snapshot());
        others.Verify(p => p.SendCoreAsync("Stroke",
            It.Is<object?[]>(a => ((Stroke)a[0]!).Color == "#ff0000"), default), Times.Once);
    }

    //перевірка, що сервер очищає штрих від некоректних даних перед розсилкою
    [Fact]
    public async Task Draw_InvalidStroke_IsSanitized()
    {
        var state = new BoardState();
        var clients = new Mock<IHubCallerClients>();
        clients.Setup(c => c.Others).Returns(new Mock<IClientProxy>().Object);
        var hub = new BoardHub(state) { Clients = clients.Object };

        await hub.Draw(new Stroke(0, 0, 1, 1, "<script>", 999));

        var stored = Assert.Single(state.Snapshot());
        Assert.Equal("#000000", stored.Color);
        Assert.Equal(30, stored.Size);
    }

    //перевірка, що очищення стирає історію і сповіщає інших учасників
    [Fact]
    public async Task Clear_EmptiesHistoryAndNotifiesOthers()
    {
        var state = new BoardState();
        state.Add(new Stroke(1, 1, 2, 2, "#000000", 1));
        var others = new Mock<IClientProxy>();
        var clients = new Mock<IHubCallerClients>();
        clients.Setup(c => c.Others).Returns(others.Object);
        var hub = new BoardHub(state) { Clients = clients.Object };

        await hub.Clear();

        Assert.Empty(state.Snapshot());
        others.Verify(p => p.SendCoreAsync("Cleared", It.IsAny<object?[]>(), default), Times.Once);
    }

    //перевірка, що новий учасник отримує історію, а всі отримують кількість людей онлайн
    [Fact]
    public async Task OnConnected_SendsHistoryToCallerAndUsersToAll()
    {
        var state = new BoardState();
        state.Add(new Stroke(1, 1, 2, 2, "#000000", 1));
        var caller = new Mock<IClientProxy>();
        var all = new Mock<IClientProxy>();
        var clients = new Mock<IHubCallerClients>();
        clients.Setup(c => c.Caller).Returns(caller.Object);
        clients.Setup(c => c.All).Returns(all.Object);
        var hub = new BoardHub(state) { Clients = clients.Object };

        await hub.OnConnectedAsync();

        caller.Verify(p => p.SendCoreAsync("History",
            It.Is<object?[]>(a => ((List<Stroke>)a[0]!).Count == 1), default), Times.Once);
        all.Verify(p => p.SendCoreAsync("Users",
            It.Is<object?[]>(a => (int)a[0]! == 1), default), Times.Once);
    }
}

public class BoardEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    public BoardEndpointsTests(CustomWebApplicationFactory factory) => _client = factory.CreateClient();

    //перевірка, що сторінка дошки відкривається і містить полотно
    [Fact]
    public async Task BoardPage_ContainsCanvas()
    {
        var response = await _client.GetAsync("/Board");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"board\"", html);
    }

    //перевірка, що хаб дошки підключений і відповідає на узгодження транспорту
    [Fact]
    public async Task BoardHub_NegotiateEndpoint_Responds()
    {
        var response = await _client.PostAsync("/hubs/board/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}