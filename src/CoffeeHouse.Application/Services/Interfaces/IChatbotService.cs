namespace CoffeeHouse.Application.Services.Interfaces
{
    public interface IChatbotService
    {
        Task<string> AskAssistantAsync(string question);
    }
}
