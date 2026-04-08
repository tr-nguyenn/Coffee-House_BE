using CoffeeHouse.Application.Interfaces;
using CoffeeHouse.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CoffeeHouse.Application.Services.Implementations
{
    public class LlmService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string OPENAI_URL = "https://api.groq.com/openai/v1/chat/completions";
        private const string MODEL = "llama-3.3-70b-versatile";

        public LlmService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _apiKey = config["AI:OpenAiApiKey"]
                ?? throw new InvalidOperationException("Chưa cấu hình AI:OpenAiApiKey trong appsettings.json!");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task<string> GenerateSqlAsync(string question, string dbSchema)
        {
            string systemPrompt = $@"
                Bạn là một chuyên gia SQL Server. 
                Sơ đồ cơ sở dữ liệu (Schema):
                {dbSchema}
                
                Nhiệm vụ: Viết câu lệnh T-SQL để lấy dữ liệu trả lời câu hỏi.
                Quy tắc TỐI THƯỢNG: 
                1. CHỈ trả về câu lệnh SQL, KHÔNG giải thích, KHÔNG dùng markdown.
                2. TÌM KIẾM TEXT: Bắt buộc dùng LIKE N'%chuỗi%' khi lọc theo tên (Name, Khu vực, Sản phẩm). Ví dụ: WHERE a.Name LIKE N'%Tầng 1%'
                3. JOIN BẮT BUỘC: Nếu cần lọc theo tên của bảng khác, PHẢI dùng JOIN dựa trên Foreign Key đã định nghĩa trong Schema. (Ví dụ: Để tìm bàn ở Tầng 1, phải JOIN Tables và Areas).
                4. NGÀY THÁNG: Dùng CAST(CộtNgàyTháng AS DATE) = CAST(GETDATE() AS DATE) cho 'hôm nay'.
                
                Ví dụ: 'Liệt kê các bàn tầng 1' -> SELECT t.* FROM Tables t JOIN Areas a ON t.AreaId = a.Id WHERE a.Name LIKE N'%Tầng 1%'";

            return await CallOpenAiApiAsync(systemPrompt, question, temperature: 0.0);
        }

        public async Task<string> GenerateHumanResponseAsync(string question, string rawJsonData)
        {
            string systemPrompt = $@"
                Bạn là trợ lý ảo quản lý quán cà phê.
                Người dùng hỏi: '{question}'
                Dữ liệu truy xuất được (JSON): '{rawJsonData}'
                
                Nhiệm vụ: Trả lời người dùng dựa trên dữ liệu JSON. 
                - Nếu JSON trống ([]), hãy nói: 'Dạ sếp, hiện tại chưa có dữ liệu hoặc không tìm thấy kết quả phù hợp ạ.'
                - Nếu có dữ liệu, hãy tóm tắt lịch sự, thêm emoji. Không nhắc đến chữ JSON hoặc Database.";

            return await CallOpenAiApiAsync(systemPrompt, "Hãy trả lời.", temperature: 0.7);
        }

        public async Task<string> ClassifyIntentAsync(string question)
        {
            string systemPrompt = @"
                Phân loại câu hỏi vào 1 trong 2 loại:
                - 'SMALL_TALK': Giao tiếp, chào hỏi, hỏi bạn là ai.
                - 'DATABASE_QUERY': Hỏi về doanh thu, bàn, đơn hàng, kho.
                Chỉ trả về 1 từ duy nhất.";
            return await CallOpenAiApiAsync(systemPrompt, question, temperature: 0.0);
        }

        public async Task<string> ChatNormalAsync(string question)
        {
            string systemPrompt = "Bạn là trợ lý ảo quán cà phê. Gọi người dùng là sếp, xưng em. Trả lời vui vẻ, ngắn gọn.";
            return await CallOpenAiApiAsync(systemPrompt, question, temperature: 0.7);
        }

        public async Task<string> FixSqlAsync(string wrongSql, string errorMessage, string dbSchema)
        {
            string systemPrompt = $@"
                Bạn là chuyên gia SQL Server.
                Sơ đồ DB: {dbSchema}
                SQL BỊ LỖI: {wrongSql}
                LỖI BÁO VỀ: {errorMessage}
                
                Nhiệm vụ: Sửa lại câu lệnh SQL.
                - Chú ý dùng LIKE N'%...%' cho chuỗi.
                - Đảm bảo JOIN đúng Foreign Key.
                Chỉ trả về SQL đã sửa, không giải thích.";
            return await CallOpenAiApiAsync(systemPrompt, "Hãy sửa lại.", temperature: 0.0);
        }

        private async Task<string> CallOpenAiApiAsync(string systemPrompt, string userMessage, double temperature)
        {
            var payload = new { model = MODEL, temperature = temperature, messages = new[] { new { role = "system", content = systemPrompt }, new { role = "user", content = userMessage } } };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(OPENAI_URL, content);
            if (!response.IsSuccessStatusCode) throw new Exception($"API Lỗi: {await response.Content.ReadAsStringAsync()}");
            using var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return jsonDoc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
        }
    }
}