using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using UtilityApi.Models; // ✅ Use your ApiResponse<T> model

namespace UtilityApi.Middlewares
{
    /// <summary>
    /// Global exception handling middleware for ASP.NET Core 8 Web API.
    /// Catches unhandled exceptions, logs them, and returns a consistent ApiResponse.
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Main entry point for the middleware.
        /// Wraps downstream requests in try-catch to capture unhandled exceptions globally.
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Proceed to the next middleware or endpoint
                await _next(context);
            }
            catch (Exception ex)
            {
                try
                {
                    // Log the unhandled exception
                    _logger.LogError(ex, "Unhandled exception occurred during request processing.");

                    // Generate a standardized API response for the client
                    await HandleExceptionAsync(context, ex);
                }
                catch (Exception innerEx)
                {
                    // 🔴 Last line of defense — even if our handler fails, app will not crash
                    _logger.LogCritical(innerEx, "Critical error occurred while handling an exception.");

                    // Respond with a minimal fallback message
                    if (!context.Response.HasStarted)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        context.Response.ContentType = "text/plain";
                        await context.Response.WriteAsync("A critical server error occurred. Please contact support.");
                    }
                }
            }
        }

        /// <summary>
        /// Handles exceptions and returns a well-structured ApiResponse.
        /// </summary>
        private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            // Prevent writing response if it's already started
            if (context.Response.HasStarted)
                return;

            context.Response.ContentType = "application/json";

            // Determine HTTP status code based on exception type
            var statusCode = ex switch
            {
                SqlException => (int)HttpStatusCode.InternalServerError,     // Database errors
                InvalidOperationException => (int)HttpStatusCode.BadRequest, // Invalid operation logic
                ArgumentNullException => (int)HttpStatusCode.BadRequest,     // Missing argument
                _ => (int)HttpStatusCode.InternalServerError                 // Default for unexpected errors
            };

            context.Response.StatusCode = statusCode;

            // ✅ Create a structured API response
            var apiResponse = ApiResponse<string>.Fail(
                message: ex.Message,
                errors: ex.InnerException?.Message ?? ex.StackTrace,
                statusCode: statusCode
            );

            // Serialize response into JSON (with indentation for readability)
            var json = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            // Write the error response back to the client
            await context.Response.WriteAsync(json);
        }
    }

    /// <summary>
    /// Extension method for easier registration of the middleware in Program.cs
    /// </summary>
    public static class ExceptionHandlingMiddlewareExtensions
    {
        /// <summary>
        /// Enables global exception handling for all endpoints.
        /// </summary>
        public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}
