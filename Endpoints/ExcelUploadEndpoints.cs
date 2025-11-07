using ClosedXML.Excel;
using Dapper;
using Microsoft.Data.SqlClient;

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

                        // ❌ Missing field values → Log error
                        if (!string.IsNullOrEmpty(missingFields))
                        {
                            await LogError(connection, "Customers", c.CustomerId, rowNumber, null, null,
                                $"Missing required field(s): {missingFields}");
                            continue;
                        }

                        // ✅ Check if Customer already exists → skip silently (no error log)
                        var exists = await connection.ExecuteScalarAsync<int>(
                            "SELECT COUNT(1) FROM Customers WHERE CustomerId = @CustomerId", new { c.CustomerId });

                        if (exists > 0)
                            continue;

                        // ✅ Insert Customer
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
                            await LogError(connection, "Products", null, rowNumber, p.ProductId, null,
                                $"Missing required field(s): {missingFields}");
                            continue;
                        }

                        var exists = await connection.ExecuteScalarAsync<int>(
                            "SELECT COUNT(1) FROM Products WHERE ProductId = @ProductId", new { p.ProductId });

                        if (exists > 0)
                            continue; // ✅ Skip duplicate silently

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
                            await LogError(connection, "Sales", s.CustomerId, rowNumber, s.ProductId, s.SaleId,
                                $"Missing required field(s): {missingFields}");
                            continue;
                        }

                        // ✅ Check FK existence
                        var validCustomer = await connection.ExecuteScalarAsync<int>(
                            "SELECT COUNT(1) FROM Customers WHERE CustomerId = @CustomerId", new { s.CustomerId });
                        var validProduct = await connection.ExecuteScalarAsync<int>(
                            "SELECT COUNT(1) FROM Products WHERE ProductId = @ProductId", new { s.ProductId });

                        if (validCustomer == 0 || validProduct == 0)
                            continue;

                        var exists = await connection.ExecuteScalarAsync<int>(
                            "SELECT COUNT(1) FROM Sales WHERE SaleId = @SaleId", new { s.SaleId });

                        if (exists > 0)
                            continue; // ✅ Skip duplicate silently

                        try
                        {
                            await connection.ExecuteAsync(
                                "INSERT INTO Sales (SaleId, CustomerId, ProductId, Quantity, Total) VALUES (@SaleId, @CustomerId, @ProductId, @Quantity, @Total)", s);
                        }
                        catch (SqlException ex)
                        {
                            if (!ex.Message.Contains("FOREIGN KEY constraint"))
                            {
                                await LogError(connection, "Sales", s.CustomerId, rowNumber, s.ProductId, s.SaleId,
                                    $"SQL Error: {ex.Message}");
                            }
                        }
                    }
                }

                return Results.Ok("Excel data processed successfully");
            })
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .WithTags("Excel Upload");
        }

        // ✅ Helper: Find Excel sheet
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

        // ✅ Helper: Log error (avoid duplicates)
        private static async Task LogError(SqlConnection connection, string tableName, string? customerId, int rowNumber, string? productId, string? saleId, string errorMessage)
        {
            string recordIdentifier = customerId ?? productId ?? saleId ?? "UNKNOWN";

            // Avoid duplicate error logs
            var exists = await connection.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1) FROM ErrorRecords 
                  WHERE TableName = @TableName 
                    AND RecordIdentifier = @RecordIdentifier
                    AND ErrorMessage = @ErrorMessage",
                new
                {
                    TableName = tableName,
                    RecordIdentifier = recordIdentifier,
                    ErrorMessage = errorMessage
                });

            if (exists > 0)
                return;

            await connection.ExecuteAsync(
                @"INSERT INTO ErrorRecords 
                  (TableName, RecordIdentifier, RowNumber, CustomerId, ProductId, SaleId, ErrorMessage)
                  VALUES (@TableName, @RecordIdentifier, @RowNumber, @CustomerId, @ProductId, @SaleId, @ErrorMessage)",
                new
                {
                    TableName = tableName,
                    RecordIdentifier = recordIdentifier,
                    RowNumber = rowNumber,
                    CustomerId = customerId,
                    ProductId = productId,
                    SaleId = saleId,
                    ErrorMessage = errorMessage
                });
        }

    }
}
