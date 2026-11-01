using TaskFlow.Web.Models;

namespace TaskFlow.Web.Services;

public static class DisplayNames
{
    public static string Status(ItemStatus s) => s switch
    {
        ItemStatus.ToDo => "До виконання",
        ItemStatus.InProgress => "У роботі",
        ItemStatus.Done => "Виконано",
        _ => s.ToString()
    };

    public static string Priority(Priority p) => p switch
    {
        Models.Priority.Low => "Низький",
        Models.Priority.Medium => "Середній",
        Models.Priority.High => "Високий",
        _ => p.ToString()
    };
}
