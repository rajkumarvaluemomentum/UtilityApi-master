using Dapper;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using UtilityApi.Models;

namespace UtilityApi.Endpoints
{
    public static class ErrorRecordEndpoints
    {
        public static void MapErrorRecordEndpoints(this WebApplication app, string connString)
        {
            // ✅ Get all error records
            app.MapGet("/Errors-Details", async () =>
            {
                using var connection = new SqlConnection(connString);

                var errors = await connection.QueryAsync<ErrorRecord>(
                    "SELECT * FROM ErrorRecords ORDER BY LoggedDate DESC");

                // Parse JSON column to object for clean response
                var result = errors.Select(e => new
                {
                    e.FileName,
                    e.TableName,
                    ErrorDetails = TryParseJson(e.ErrorDetails),
                    e.LoggedDate
                });

                return Results.Json(new
                {
                    statusCode = 200,
                    success = true,
                    message = "All error records retrieved successfully.",
                    data = result
                });
            })
            .WithSummary("Get all error records")
            .WithTags("Error Records");


            // ✅ Get Errors by FileName and TableName
            app.MapGet("/Errors-Details/{fileName}/{tableName}", async (string fileName, string tableName) =>
            {
                if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(tableName))
                    return Results.BadRequest(new
                    {
                        statusCode = 400,
                        success = false,
                        message = "Filename and Table name are required."
                    });

                using var connection = new SqlConnection(connString);

                var query = @"SELECT  FileName, TableName, ErrorDetails, LoggedDate
                              FROM ErrorRecords
                              WHERE FileName = @FileName AND TableName = @TableName
                              ORDER BY LoggedDate DESC";

                var errors = await connection.QueryAsync<ErrorRecord>(query, new { FileName = fileName, TableName = tableName });

                if (!errors.Any())
                {
                    return Results.Json(new
                    {
                        statusCode = 404,
                        success = false,
                        message = $"No error records found for File '{fileName}' and Table '{tableName}'."
                    });
                }

                // Deserialize JSON string into structured objects
                var parsed = errors.Select(e => new
                {
                    e.FileName,
                    e.TableName,
                    ErrorDetails = TryParseJson(e.ErrorDetails),
                    e.LoggedDate
                });

                return Results.Json(new
                {
                    statusCode = 200,
                    success = true,
                    message = "Error records retrieved successfully.",
                    data = parsed
                });
            })
            .WithSummary("Get error records by File name and Table name")
            .WithTags("Error Records");
        }

        // ✅ Helper method for safe JSON parsing
        private static object? TryParseJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<object>(json);
            }
            catch
            {
                return json; // fallback if not valid JSON
            }
        }
    }
}
