namespace CoffeeHouse.Application.Services.Interfaces
{
    public interface ILlmService
    {
        Task<string> GenerateSqlAsync(string question, string dbSchema);
        Task<string> GenerateHumanResponseAsync(string question, string rawJsonData);
        Task<string> ClassifyIntentAsync(string question);
        Task<string> ChatNormalAsync(string question);
        Task<string> FixSqlAsync(string wrongSql, string errorMessage, string dbSchema);
    }
}
