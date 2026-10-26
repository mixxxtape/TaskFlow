namespace TaskFlow.Web.Services;

public interface IFileStorage
{
    /// <summary>Зберігає файл і повертає URL, який кладемо в AttachmentUrl.</summary>
    Task<string> SaveAsync(IFormFile file);

    /// <summary>Видаляє раніше збережений файл (ігнорує чужі/порожні URL).</summary>
    void Delete(string? url);
}
