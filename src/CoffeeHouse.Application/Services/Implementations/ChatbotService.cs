using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Application.Services.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeHouse.Application.Services.Implementations
{
    public class ChatbotService : IChatbotService
    {
        private readonly ILlmService _llmService;
        private readonly string _readOnlyConnectionString;
        private readonly IServiceProvider _serviceProvider;
        private static string _cachedSchema = string.Empty;

        public ChatbotService(ILlmService llmService, IServiceProvider serviceProvider, IConfiguration config)
        {
            _llmService = llmService;
            _serviceProvider = serviceProvider;
            _readOnlyConnectionString = config.GetConnectionString("ChatbotReadOnlyConnection")
                ?? throw new InvalidOperationException("Chưa cấu hình ChatbotReadOnlyConnection trong appsettings.json!");
        }

        public async Task<string> AskAssistantAsync(string question)
        {
            if (string.IsNullOrEmpty(_cachedSchema))
            {
                _cachedSchema = BuildDynamicSchema();
                Console.WriteLine($"[SCHEMA GENERATED]: \n{_cachedSchema}"); // Keep this for your debugging
            }

            string intent = await _llmService.ClassifyIntentAsync(question);
            if (intent.ToUpper().Contains("SMALL_TALK"))
            {
                return await _llmService.ChatNormalAsync(question);
            }

            string sqlQuery = await _llmService.GenerateSqlAsync(question, _cachedSchema);
            string rawJsonData = string.Empty;
            int maxRetries = 3;
            bool success = false;

            for (int retry = 0; retry < maxRetries; retry++)
            {
                sqlQuery = sqlQuery.Replace("```sql", "").Replace("```", "").Trim();
                Console.WriteLine($"[TRY {retry + 1} SQL]: {sqlQuery}");

                if (IsHarmfulSql(sqlQuery)) return "Xin lỗi sếp, lệnh này không an toàn!";

                try
                {
                    using (var connection = new SqlConnection(_readOnlyConnectionString))
                    {
                        var dbResult = await connection.QueryAsync<dynamic>(sqlQuery);
                        rawJsonData = JsonSerializer.Serialize(dbResult);
                        Console.WriteLine($"[RESULT DATA]: {rawJsonData}");

                        // CRITICAL FIX: If the result is an empty array, trigger a retry.
                        if (rawJsonData == "[]" || string.IsNullOrWhiteSpace(rawJsonData))
                        {
                            // Tell the LLM it returned no data, try broadening the search
                            string emptyDataError = "Lệnh SQL chạy thành công nhưng không trả về dữ liệu ([]). Hãy thử dùng toán tử LIKE N'%...%' thay vì = cho các chuỗi, hoặc kiểm tra lại điều kiện JOIN.";
                            if (retry == maxRetries - 1) break; // Give up on last try
                            sqlQuery = await _llmService.FixSqlAsync(sqlQuery, emptyDataError, _cachedSchema);
                            continue;
                        }

                        success = true;
                        break;
                    }
                }
                catch (SqlException ex)
                {
                    Console.WriteLine($"[SQL EXCEPTION]: {ex.Message}");
                    if (retry == maxRetries - 1) break;
                    sqlQuery = await _llmService.FixSqlAsync(sqlQuery, ex.Message, _cachedSchema);
                }
            }

            if (!success)
            {
                // Let the human response generator handle the empty data gracefully
                return await _llmService.GenerateHumanResponseAsync(question, "[]");
            }

            return await _llmService.GenerateHumanResponseAsync(question, rawJsonData);
        }

        private string BuildDynamicSchema()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContextType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == "ApplicationDbContext");

            if (dbContextType == null) return "Schema Unidentified";
            var dbContext = scope.ServiceProvider.GetService(dbContextType) as DbContext;
            if (dbContext == null) return "Schema Unidentified";

            var schemaBuilder = new System.Text.StringBuilder();

            foreach (var entityType in dbContext.Model.GetEntityTypes())
            {
                var tableName = entityType.GetTableName();
                if (string.IsNullOrEmpty(tableName) || tableName.StartsWith("AppUser") || tableName.StartsWith("AppRole"))
                    continue;

                var columns = entityType.GetProperties()
                    .Select(p =>
                    {
                        var colName = p.GetColumnName() ?? p.Name;
                        var colType = p.GetColumnType() ?? p.ClrType.Name;
                        if (colType.Contains("String")) colType = "NVARCHAR";
                        return $"{colName} {colType}";
                    }).ToList();

                schemaBuilder.AppendLine($"Table {tableName} ({string.Join(", ", columns)})");

                var foreignKeys = entityType.GetForeignKeys();
                foreach (var fk in foreignKeys)
                {
                    var fkProps = string.Join(", ", fk.Properties.Select(p => p.GetColumnName() ?? p.Name));
                    var pkTableName = fk.PrincipalEntityType.GetTableName();
                    var pkProps = string.Join(", ", fk.PrincipalKey.Properties.Select(p => p.GetColumnName() ?? p.Name));
                    schemaBuilder.AppendLine($"- [Foreign Key] {tableName}.({fkProps}) references {pkTableName}.({pkProps})");
                }
            }
            return schemaBuilder.ToString();
        }

        private bool IsHarmfulSql(string sql)
        {
            string pattern = @"\b(INSERT|UPDATE|DELETE|DROP|ALTER|TRUNCATE|EXEC|CREATE|GRANT|REVOKE)\b";
            return Regex.IsMatch(sql, pattern, RegexOptions.IgnoreCase);
        }
    }
}