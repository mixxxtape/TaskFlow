using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models;

public class Project
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Вкажіть назву"), StringLength(150)]
    [Display(Name = "Назва")]
    public string Name { get; set; } = "";

    [StringLength(2000)]
    [Display(Name = "Опис")]
    public string? Description { get; set; }

    [Display(Name = "Створено")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(500)]
    public string? CoverImageUrl { get; set; }

    [EmailAddress(ErrorMessage = "Невірний формат email"), StringLength(200)]
    [Display(Name = "Контактний email")]
    public string? ContactEmail { get; set; }

    public List<TaskItem> Tasks { get; set; } = new();
}
