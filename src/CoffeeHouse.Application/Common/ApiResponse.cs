namespace CoffeeHouse.Application.Common
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public static ApiResponse<T> SuccessResult(T data, string message = "Thành công")
            => new() { Success = true, Data = data, Message = message };

        public static ApiResponse<T> FailureResult(string message)
            => new() { Success = false, Message = message };
    }
}
