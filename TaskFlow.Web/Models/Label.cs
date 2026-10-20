namespace TaskFlow.Web.Models;

public class Label
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#681ceb";
    public List<TaskItem> Tasks { get; set; } = new();
}