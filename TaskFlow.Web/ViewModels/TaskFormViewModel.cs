using System.ComponentModel.DataAnnotations;
using TaskFlow.Web.Models;

namespace TaskFlow.Web.ViewModels;

public class TaskFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter a title"), StringLength(200)]
    [Display(Name = "Title")]
    public string Title { get; set; } = "";

    [StringLength(4000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Status")]
    public ItemStatus Status { get; set; } = ItemStatus.ToDo;

    [Display(Name = "Пріоритет")]
    public Priority Priority { get; set; } = Priority.Medium;

    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Deadline")]
    public DateTime? DueDate { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a project")]
    [Display(Name = "Project")]
    public int ProjectId { get; set; }

    [Display(Name = "Assignee")]
    public int? AssigneeId { get; set; }

    [Display(Name = "Labels")]
    public List<int> LabelIds { get; set; } = new();

    [Display(Name = "Attachment")]
    public IFormFile? Attachment { get; set; }

    public string? ExistingAttachmentUrl { get; set; }
}
