namespace TaskFlow.Web.Models;

public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.ToDo;
    public Priority Priority { get; set; } = Priority.Medium;
    public DateTime? DueDate { get; set; }
    public string? AttachmentUrl { get; set; }  

    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int? AssigneeId { get; set; }
    public AppUser? Assignee { get; set; }

    public List<Comment> Comments { get; set; } = new();
    public List<Label> Labels { get; set; } = new();
}