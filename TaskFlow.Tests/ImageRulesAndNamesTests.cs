using TaskFlow.Web.Models;
using TaskFlow.Web.Services;

namespace TaskFlow.Tests;

public class ImageRulesAndNamesTests
{
    //перевірка, що дозволеними вважаються лише зображення з підтримуваними розширеннями
    [Theory]
    [InlineData("photo.png", true)]
    [InlineData("photo.JPG", true)]
    [InlineData("photo.jpeg", true)]
    [InlineData("anim.gif", true)]
    [InlineData("doc.pdf", false)]
    [InlineData("virus.exe", false)]
    [InlineData("noextension", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsImage_RecognisesOnlyImageExtensions(string? name, bool expected) =>
        Assert.Equal(expected, ImageRules.IsImage(name));


    //перевірка, що статуси завдань мають правильні українські назви
    [Theory]
    [InlineData(ItemStatus.ToDo, "До виконання")]
    [InlineData(ItemStatus.InProgress, "У роботі")]
    [InlineData(ItemStatus.Done, "Виконано")]
    public void Status_HasUkrainianName(ItemStatus status, string expected) =>
        Assert.Equal(expected, DisplayNames.Status(status));


    //перевірка, що пріоритети завдань мають правильні українські назви
    [Theory]
    [InlineData(Priority.Low, "Низький")]
    [InlineData(Priority.Medium, "Середній")]
    [InlineData(Priority.High, "Високий")]
    public void Priority_HasUkrainianName(Priority priority, string expected) =>
        Assert.Equal(expected, DisplayNames.Priority(priority));
}
