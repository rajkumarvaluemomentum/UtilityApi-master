using ClosedXML.Excel;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using UtilityApi.Models;

namespace UtilityApi.Endpoints
{
    public static class ExcelUploadEndpoints
    {
        public static void MapExcelUploadEndpoints(this WebApplication app, string connString)
        {
            app.MapPost("/upload-excel", async (IFormFile file) =>
            {
                if (file == null || file.Length == 0)
                    return Results.BadRequest("Please upload a valid Excel file.");

                string fileName = file.FileName;
                var allErrors = new List<object>();

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                using var workbook = new XLWorkbook(stream);
                using var connection = new SqlConnection(connString);
                await connection.OpenAsync();

                // ✅ Process Customers
                if (TryGetSheet(workbook, "Customers", out var customerSheet))
                {
                    string currentTable = "Customers";
                    var tableErrors = new List<object>();

                    int rowNumber = 1;
                    foreach (var row in customerSheet.RowsUsed().Skip(1))
                    {
                        rowNumber++;
                        var c = new Customer
                        {
                            CustomerId = row.Cell(1).GetString(),
                            Name = row.Cell(2).GetString(),
                            Email = row.Cell(3).GetString(),
                            Phone = row.Cell(4).GetString()
                        };

                        var missingFields = GetMissingFields(
                            ("CustomerId", c.CustomerId),
                            ("Name", c.Name),
                            ("Email", c.Email),
                            ("Phone", c.Phone)
                        );

                        if (!string.IsNullOrEmpty(missingFields))
                        {
                            tableErrors.Add(new { Row = rowNumber, Error = $"Missing required field(s): {missingFields}" });
                            continue;
                        }

                        try
                        {
                            var exists = await connection.ExecuteScalarAsync<int>(
                                "SELECT COUNT(1) FROM Customers WHERE CustomerId = @CustomerId", new { c.CustomerId });

                            if (exists == 0)
                            {
                                await connection.ExecuteAsync(
                                    "INSERT INTO Customers (CustomerId, Name, Email, Phone) VALUES (@CustomerId, @Name, @Email, @Phone)", c);
                            }
                        }
                        catch (Exception ex)
                        {
                            tableErrors.Add(new { Row = rowNumber, Error = ex.Message });
                        }
                    }

                    // ✅ Log to DB if any errors
                    if (tableErrors.Any())
                        await LogError(connection, fileName, currentTable, tableErrors);
                    allErrors.AddRange(tableErrors);
                }

                // ✅ Process Products
                if (TryGetSheet(workbook, "Products", out var productSheet))
                {
                    string currentTable = "Products";
                    var tableErrors = new List<object>();

                    int rowNumber = 1;
                    foreach (var row in productSheet.RowsUsed().Skip(1))
                    {
                        rowNumber++;
                        var p = new Product
                        {
                            ProductId = row.Cell(1).GetString(),
                            ProductName = row.Cell(2).GetString(),
                            Category = row.Cell(3).GetString(),
                            Price = row.Cell(4).TryGetValue<decimal>(out var price) ? price : (decimal?)null
                        };

                        var missingFields = GetMissingFields(
                            ("ProductId", p.ProductId),
                            ("ProductName", p.ProductName),
                            ("Category", p.Category),
                            ("Price", p.Price)
                        );

                        if (!string.IsNullOrEmpty(missingFields))
                        {
                            tableErrors.Add(new { Row = rowNumber, Error = $"Missing required field(s): {missingFields}" });
                            continue;
                        }

                        try
                        {
                            var exists = await connection.ExecuteScalarAsync<int>(
                                "SELECT COUNT(1) FROM Products WHERE ProductId = @ProductId", new { p.ProductId });

                            if (exists == 0)
                            {
                                await connection.ExecuteAsync(
                                    "INSERT INTO Products (ProductId, ProductName, Category, Price) VALUES (@ProductId, @ProductName, @Category, @Price)", p);
                            }
                        }
                        catch (Exception ex)
                        {
                            tableErrors.Add(new { Row = rowNumber, Error = ex.Message });
                        }
                    }

                    if (tableErrors.Any())
                        await LogError(connection, fileName, currentTable, tableErrors);
                    allErrors.AddRange(tableErrors);
                }

                // ✅ Process Sales
                if (TryGetSheet(workbook, "Sales", out var salesSheet))
                {
                    string currentTable = "Sales";
                    var tableErrors = new List<object>();

                    int rowNumber = 1;
                    foreach (var row in salesSheet.RowsUsed().Skip(1))
                    {
                        rowNumber++;
                        var s = new Sale
                        {
                            SaleId = row.Cell(1).GetString(),
                            CustomerId = row.Cell(2).GetString(),
                            ProductId = row.Cell(3).GetString(),
                            Quantity = row.Cell(4).TryGetValue<int>(out var q) ? q : (int?)null,
                            Total = row.Cell(5).TryGetValue<decimal>(out var t) ? t : (decimal?)null
                        };

                        var missingFields = GetMissingFields(
                            ("SaleId", s.SaleId),
                            ("CustomerId", s.CustomerId),
                            ("ProductId", s.ProductId),
                            ("Quantity", s.Quantity),
                            ("Total", s.Total)
                        );

                        if (!string.IsNullOrEmpty(missingFields))
                        {
                            tableErrors.Add(new { Row = rowNumber, Error = $"Missing required field(s): {missingFields}" });
                            continue;
                        }

                        try
                        {
                            var validCustomer = await connection.ExecuteScalarAsync<int>(
                                "SELECT COUNT(1) FROM Customers WHERE CustomerId = @CustomerId", new { s.CustomerId });
                            var validProduct = await connection.ExecuteScalarAsync<int>(
                                "SELECT COUNT(1) FROM Products WHERE ProductId = @ProductId", new { s.ProductId });

                            if (validCustomer == 0 || validProduct == 0)
                            {
                                tableErrors.Add(new { Row = rowNumber, Error = "The referenced Customer or Product does not exist." });
                                continue;
                            }

                            var exists = await connection.ExecuteScalarAsync<int>(
                                "SELECT COUNT(1) FROM Sales WHERE SaleId = @SaleId", new { s.SaleId });

                            if (exists == 0)
                            {
                                await connection.ExecuteAsync(
                                    "INSERT INTO Sales (SaleId, CustomerId, ProductId, Quantity, Total) VALUES (@SaleId, @CustomerId, @ProductId, @Quantity, @Total)", s);
                            }
                        }
                        catch (Exception ex)
                        {
                            tableErrors.Add(new { Row = rowNumber, Error = ex.Message });
                        }
                    }

                    if (tableErrors.Any())
                        await LogError(connection, fileName, currentTable, tableErrors);
                    allErrors.AddRange(tableErrors);
                }

                // ✅ Final Response
                return Results.Json(new
                {
                    statusCode = 200,
                    success = true,
                    message = "Excel data processed successfully.",
                    data = new
                    {
                        file = fileName,
                        totalErrors = allErrors.Count,
                        errorDetails = allErrors
                    }
                });
            })
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .WithTags("Excel Upload")
            .WithSummary("Upload Excel Data Into Database");
        }

        // ✅ Helper: Insert error record
        private static async Task LogError(SqlConnection connection, string fileName, string tableName, List<object> tableErrors)
        {
            string errorJson = JsonSerializer.Serialize(tableErrors);

            var errorRecord = new ErrorRecord
            {
                FileName = fileName,
                TableName = tableName,
                ErrorDetails = errorJson,
                LoggedDate = DateTime.UtcNow
            };

            const string insertQuery = @"
                INSERT INTO ErrorRecords (FileName, TableName, ErrorDetails, LoggedDate)
                VALUES (@FileName, @TableName, @ErrorDetails, @LoggedDate);";

            await connection.ExecuteAsync(insertQuery, errorRecord);
        }

        // ✅ Helper: Check sheet existence
        private static bool TryGetSheet(XLWorkbook workbook, string name, out IXLWorksheet sheet)
        {
            sheet = workbook.Worksheets.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return sheet != null;
        }

        // ✅ Helper: Detect missing fields
        private static string GetMissingFields(params (string FieldName, object? Value)[] fields)
        {
            var missing = fields
                .Where(f => f.Value == null || (f.Value is string s && string.IsNullOrWhiteSpace(s)))
                .Select(f => f.FieldName)
                .ToList();

            return missing.Count > 0 ? string.Join(", ", missing) : string.Empty;
        }
    }
}
