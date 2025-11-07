using ClosedXML.Excel;

namespace UtilityApi.Endpoints
{
    public static class ExcelGeneratorEndpoints
    {
        public static void MapExcelGeneratorEndpoints(this WebApplication app)
        {
            app.MapGet("/generate-excel", async (HttpContext context) =>
            {
                string fileName = $"SampleData_2000Records_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                string filePath = Path.Combine(Path.GetTempPath(), fileName);

                int numRecords = 2000;
                var random = new Random();

                using (var workbook = new XLWorkbook())
                {
                    // Customers Sheet
                    var wsCustomers = workbook.Worksheets.Add("Customers");
                    wsCustomers.Cell(1, 1).Value = "CustomerId";
                    wsCustomers.Cell(1, 2).Value = "Name";
                    wsCustomers.Cell(1, 3).Value = "Email";
                    wsCustomers.Cell(1, 4).Value = "Phone";

                    for (int i = 1; i <= numRecords; i++)
                    {
                        wsCustomers.Cell(i + 1, 1).Value = $"C{i:0000}";
                        wsCustomers.Cell(i + 1, 2).Value = $"Customer {i}";
                        wsCustomers.Cell(i + 1, 3).Value = $"customer{i}@example.com";
                        wsCustomers.Cell(i + 1, 4).Value = $"98765{i % 10000:0000}";
                    }

                    // Products Sheet
                    var wsProducts = workbook.Worksheets.Add("Products");
                    wsProducts.Cell(1, 1).Value = "ProductId";
                    wsProducts.Cell(1, 2).Value = "ProductName";
                    wsProducts.Cell(1, 3).Value = "Category";
                    wsProducts.Cell(1, 4).Value = "Price";

                    for (int i = 1; i <= numRecords; i++)
                    {
                        wsProducts.Cell(i + 1, 1).Value = $"P{i:0000}";
                        wsProducts.Cell(i + 1, 2).Value = $"Product {i}";
                        wsProducts.Cell(i + 1, 3).Value = $"Category {i % 10}";
                        wsProducts.Cell(i + 1, 4).Value = random.Next(10, 1000);
                    }

                    // Sales Sheet
                    var wsSales = workbook.Worksheets.Add("Sales");
                    wsSales.Cell(1, 1).Value = "SaleId";
                    wsSales.Cell(1, 2).Value = "CustomerId";
                    wsSales.Cell(1, 3).Value = "ProductId";
                    wsSales.Cell(1, 4).Value = "Quantity";
                    wsSales.Cell(1, 5).Value = "Total";

                    for (int i = 1; i <= numRecords; i++)
                    {
                        wsSales.Cell(i + 1, 1).Value = $"S{i:0000}";
                        wsSales.Cell(i + 1, 2).Value = $"C{random.Next(1, numRecords):0000}";
                        wsSales.Cell(i + 1, 3).Value = $"P{random.Next(1, numRecords):0000}";
                        int quantity = random.Next(1, 10);
                        decimal price = random.Next(100, 5000);
                        wsSales.Cell(i + 1, 4).Value = quantity;
                        wsSales.Cell(i + 1, 5).Value = quantity * price;
                    }

                    workbook.SaveAs(filePath);
                }

                context.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                context.Response.Headers.Append("Content-Disposition", $"attachment; filename={fileName}");
                await context.Response.SendFileAsync(filePath);

            })
            .WithTags("Excel Generator")
            .WithSummary("Generate Excel File with 2000 Records")
            .WithDescription("Creates an Excel file with 2000 rows in Customers, Products, and Sales sheets.");
        }
    }
}
