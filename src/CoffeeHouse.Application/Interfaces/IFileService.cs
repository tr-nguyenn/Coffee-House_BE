using Microsoft.AspNetCore.Http;

namespace CoffeeHouse.Application.Interfaces
{
    public interface IFileService
    {
        Task<string> SaveFileAsync(IFormFile fromFile, string folderName);
        void DeleteFile(string? filePath);
    }
}
