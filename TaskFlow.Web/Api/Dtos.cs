using System.ComponentModel.DataAnnotations;
using TaskFlow.Web.Models;

namespace TaskFlow.Web.Api;

public record ProjectDto(int Id, string Name, string? Description, DateTime CreatedAt, int TaskCount);

public class ProjectInput
{
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [StringLength(2000)] public string? Description { get; set; }
}

public record UserDto(int Id, string Name, string Email, string? AvatarUrl);

public class UserInput
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = "";
    [StringLength(500)] public string? AvatarUrl { get; set; }
}

public record LabelDto(int Id, string Name, string Color);

public class LabelInput
{
    [Required, StringLength(50)] public string Name { get; set; } = "";
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Колір у форматі #RRGGBB")]
    public string Color { get; set; } = "#69a1fa";
}

public record TaskDto(
    int Id, string Title, string? Description, ItemStatus Status, Priority Priority,
    DateTime? DueDate, string? AttachmentUrl, int ProjectId, int? AssigneeId, List<int> LabelIds);

public class TaskInput
{
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [StringLength(4000)] public string? Description { get; set; }
    public ItemStatus Status { get; set; } = ItemStatus.ToDo;
    public Priority Priority { get; set; } = Priority.Medium;
    public DateTime? DueDate { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "projectId обов'язковий")] public int ProjectId { get; set; }
    public int? AssigneeId { get; set; }
    public List<int> LabelIds { get; set; } = new();
}

public record CommentDto(int Id, string Text, DateTime CreatedAt, int AuthorId, string? AuthorName);

public class CommentInput
{
    [Required, StringLength(2000)] public string Text { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "authorId обов'язковий")] public int AuthorId { get; set; }
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Skip, int Limit, string? NextLink);
