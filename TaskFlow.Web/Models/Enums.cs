using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models;

public enum ItemStatus
{
    [Display(Name = "До виконання")] ToDo,
    [Display(Name = "У роботі")] InProgress,
    [Display(Name = "Виконано")] Done
}

public enum Priority
{
    [Display(Name = "Низький")] Low,
    [Display(Name = "Середній")] Medium,
    [Display(Name = "Високий")] High
}
