using ClosedXML.Excel;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Text.Json;

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

                var errorSummary = new List<object>();
                string fileName = file.FileName;

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                using var workbook = new XLWorkbook(stream);
                using var connection = new SqlConnection(connString);
                await connection.OpenAsync();

                // ✅ Process Customers
                if (TryGetSheet(workbook, "Customers", out var customerSheet))
                {
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
                            await LogError(connection, fileName, "Customers", rowNumber, c, $"Missing required field(s): {missingFields}");
                            errorSummary.Add(new { Table = "Customers", Row = rowNumber, Error = missingFields });
                            continue;
                        }

                        var exists = await connection.ExecuteScalarAsync<int>(
                            "SELECT COUNT(1) FROM Customers WHERE CustomerId = @CustomerId", new { c.CustomerId });

                        if (exists > 0)
                            continue;

                        await connection.ExecuteAsync(
                            "INSERT INTO Customers (CustomerId, Name, Email, Phone) VALUES (@CustomerId, @Name, @Email, @Phone)", c);
                    }
                }

                // ✅ Process Products
                if (TryGetSheet(workbook, "Products", out var productSheet))
                {
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
                            await LogError(connection, fileName, "Products", rowNumber, p, $"Missing required field(s): {missingFields}");
                            errorSummary.Add(new { Table = "Products", Row = rowNumber, Error = missingFields });
                            continue;
                        }

                        var exists = await connection.ExecuteScalarAsync<int>(
                            "SELECT COUNT(1) FROM Products WHERE ProductId = @ProductId", new { p.ProductId });

                        if (exists > 0)
                            continue;

                        await connection.ExecuteAsync(
                            "INSERT INTO Products (ProductId, ProductName, Category, Price) VALUES (@ProductId, @ProductName, @Category, @Price)", p);
                    }
                }

                // ✅ Process Sales
                if (TryGetSheet(workbook, "Sales", out var salesSheet))
                {
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
                            await LogError(connection, fileName, "Sales", rowNumber, s, $"Missing required field(s): {missingFields}");
                            errorSummary.Add(new { Table = "Sales", Row = rowNumber, Error = missingFields });
                            continue;
                        }

                        var validCustomer = await connection.ExecuteScalarAsync<int>(
                            "SELECT COUNT(1) FROM Customers WHERE CustomerId = @CustomerId", new { s.CustomerId });
                        var validProduct = await connection.ExecuteScalarAsync<int>(
                            "SELECT COUNT(1) FROM Products WHERE ProductId = @ProductId", new { s.ProductId });

                        if (validCustomer == 0 || validProduct == 0)
                        {
                            // await LogError(connection, fileName, "Sales", rowNumber, s, "Invalid foreign key reference (Customer or Product missing).");
                            // errorSummary.Add(new { Table = "Sales", Row = rowNumber, Error = "Invalid foreign key reference" });
                            continue;
                        }

                        var exists = await connection.ExecuteScalarAsync<int>(
                            "SELECT COUNT(1) FROM Sales WHERE SaleId = @SaleId", new { s.SaleId });

                        if (exists > 0)
                            continue;

                        try
                        {
                            await connection.ExecuteAsync(
                                "INSERT INTO Sales (SaleId, CustomerId, ProductId, Quantity, Total) VALUES (@SaleId, @CustomerId, @ProductId, @Quantity, @Total)", s);
                        }
                        catch (SqlException ex)
                        {
                            await LogError(connection, fileName, "Sales", rowNumber, s, $"SQL Error: {ex.Message}");
                            errorSummary.Add(new { Table = "Sales", Row = rowNumber, Error = ex.Message });
                        }
                    }
                }

                return Results.Json(new
                {
                    Message = "Excel data processed successfully",
                    File = fileName,
                    ErrorsLogged = errorSummary.Count,
                    ErrorDetails = errorSummary
                });
            })
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .WithTags("Excel Upload")
            .WithSummary("Upload Excel Data Into Database");
        }

        private static bool TryGetSheet(XLWorkbook workbook, string name, out IXLWorksheet sheet)
        {
            sheet = workbook.Worksheets.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return sheet != null;
        }

        private static string GetMissingFields(params (string FieldName, object? Value)[] fields)
        {
            var missing = fields
                .Where(f => f.Value == null || (f.Value is string s && string.IsNullOrWhiteSpace(s)))
                .Select(f => f.FieldName)
                .ToList();

            return missing.Count > 0 ? string.Join(", ", missing) : string.Empty;
        }
        // ✅ Log Error only if it doesn't already exist
        private static async Task LogError(SqlConnection connection, string fileName, string tableName, int rowNumber, object rowData, string errorMessage)
        {
            string jsonData = JsonSerializer.Serialize(new
            {
                ErrorDetails = errorMessage,
                RowData = rowData
            }, new JsonSerializerOptions { WriteIndented = true });

            // Check if this exact error already exists
            var exists = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1)
          FROM ErrorRecords
          WHERE FileName = @FileName AND TableName = @TableName AND RowNumber = @RowNumber",
                new { FileName = fileName, TableName = tableName, RowNumber = rowNumber });

            if (exists == 0)
            {
                await connection.ExecuteAsync(
                    @"INSERT INTO ErrorRecords (FileName, TableName, RowNumber, ErrorDetails, LoggedDate)
              VALUES (@FileName, @TableName, @RowNumber, @ErrorDetails, GETDATE())",
                    new
                    {
                        FileName = fileName,
                        TableName = tableName,
                        RowNumber = rowNumber,
                        ErrorDetails = jsonData
                    });
            }
        }


    }

}
