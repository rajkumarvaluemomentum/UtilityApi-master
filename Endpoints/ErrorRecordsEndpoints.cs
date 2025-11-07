using Dapper;
using Microsoft.Data.SqlClient;

namespace UtilityApi.Endpoints
{
    public static class ErrorRecordsEndpoints
    {
        public static void MapErrorRecordEndpoints(this WebApplication app, string connString)
        {
            // ✅ GET specific columns from ErrorRecords
            app.MapGet("/ErrorDetails", async (string? tableName) =>
            {
                using var connection = new SqlConnection(connString);
                await connection.OpenAsync();

                // ✅ Select only important columns
                string query = @"
                    SELECT 
                        TableName,
                        RowNumber,
                        RecordIdentifier,
                        CustomerId,
                        ProductId,
                        SaleId,
                        ErrorMessage
                    FROM ErrorRecords";

                // ✅ Optional table filter
                if (!string.IsNullOrWhiteSpace(tableName))
                {
                    query += " WHERE TableName = @TableName";
                    var filtered = await connection.QueryAsync(query, new { TableName = tableName });
                    return Results.Ok(filtered);
                }

                var allErrors = await connection.QueryAsync(query);
                return Results.Ok(allErrors);
            })
            .WithTags("Error Details")
            .WithSummary("Get missing  data from Exel");
        }
    }
}
