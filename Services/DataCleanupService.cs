using Microsoft.Data.SqlClient;
using System.Data;

namespace UtilityApi.Services
{
    /// <summary>
    /// Background service that automatically triggers SQL stored procedure
    /// (sp_CleanupOldRecords) at configured intervals.
    /// </summary>
    public class DataCleanupService : BackgroundService
    {
        private readonly ILogger<DataCleanupService> _logger;
        private readonly IConfiguration _config;
        private readonly string _connectionString;
        private readonly int _intervalHours;

        public DataCleanupService(ILogger<DataCleanupService> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
            _connectionString = _config.GetConnectionString("DefaultConnection");
            _intervalHours = _config.GetValue<int>("DataCleanup:IntervalHours", 24); // default every 24 hours
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Data cleanup service initialized. Runs every {hours} hour(s).", _intervalHours);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Triggering cleanup procedure (sp_CleanupOldRecords)...");

                    using var conn = new SqlConnection(_connectionString);
                    using var cmd = new SqlCommand("sp_CleanupOldRecords", conn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };

                    await conn.OpenAsync(stoppingToken);
                    await cmd.ExecuteNonQueryAsync(stoppingToken);

                    _logger.LogInformation(" Cleanup completed successfully at {time}.", DateTime.Now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cleanup procedure failed at {time}.", DateTime.Now);
                }

                // Wait for next cycle
                await Task.Delay(TimeSpan.FromHours(_intervalHours), stoppingToken);
            }
        }
    }
}
