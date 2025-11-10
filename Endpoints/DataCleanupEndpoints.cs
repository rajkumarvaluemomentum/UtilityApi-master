using Microsoft.Data.SqlClient;
using System.Data;

namespace UtilityApi.Endpoints
{
    /// <summary>
    /// Provides endpoints to manually trigger database cleanup.
    /// Uses stored procedure sp_CleanupOldRecords.
    /// </summary>
    public static class DataCleanupEndpoints
    {
        /// <summary>
        /// Maps cleanup endpoints globally.
        /// </summary>
        public static void MapDataCleanupEndpoints(this WebApplication app, string connString)
        {
            //  POST endpoint to manually trigger cleanup
            app.MapPost("/trigger-cleanup", async () =>
            {
                try
                {
                    using var connection = new SqlConnection(connString);
                    using var cmd = new SqlCommand("sp_CleanupOldRecords", connection)
                    {
                        CommandType = CommandType.StoredProcedure
                    };

                    await connection.OpenAsync();
                    await cmd.ExecuteNonQueryAsync();

                    return Results.Ok(new
                    {
                        Success = true,
                        Message = "Database cleanup executed successfully.",
                        Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    return Results.Json(new
                    {
                        Success = false,
                        Message = "Cleanup failed.",
                        Exception = ex.Message,
                        Timestamp = DateTime.UtcNow
                    }, statusCode: StatusCodes.Status500InternalServerError);
                }
            })
            .WithTags("Maintenance")
            .WithSummary("Triggers database cleanup manually.")
            .WithDescription("Executes stored procedure 'sp_CleanupOldRecords' to delete old records from multiple tables.")
            .WithOpenApi(); // for Swagger visibility
        }
    }
}
