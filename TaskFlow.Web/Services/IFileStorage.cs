namespace TaskFlow.Web.Services;

public interface IFileStorage
{
    Task<string> SaveAsync(IFormFile file);

    void Delete(string? url);
}
