using UtilityApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// ✅ Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<UtilityApi.Services.DataCleanupService>();


var app = builder.Build();

// ✅ Configure Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
var connString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                 throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

app.UseHttpsRedirection();


// ✅ Register Endpoints
app.MapExcelUploadEndpoints(connString);


// ✅ Add ErrorRecords GET endpoint
app.MapErrorRecordEndpoints(connString);
// ✅ Map the Excel endpoint
app.MapExcelGeneratorEndpoints();
app.MapDataCleanupEndpoints(connString);

app.Run();
