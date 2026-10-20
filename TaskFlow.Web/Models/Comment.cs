namespace TaskFlow.Web.Models;

public class Comment
{
	public int Id { get; set; }
	public string Text { get; set; } = "";
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public int TaskItemId { get; set; }
	public TaskItem? TaskItem { get; set; }
	public int AuthorId { get; set; }
	public AppUser? Author { get; set; }
}