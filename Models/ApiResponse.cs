using System.Text.Json.Serialization;

namespace UtilityApi.Models
{
    public class ApiResponse<T>
    {
        public int StatusCode { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Errors { get; set; }

        public static ApiResponse<T> Ok(T data, string message = "Request successful") =>
            new() { StatusCode = 200, Success = true, Message = message, Data = data };

        public static ApiResponse<T> Fail(string message, object? errors = null, int statusCode = 400) =>
            new() { StatusCode = statusCode, Success = false, Message = message, Errors = errors };
    }
}
