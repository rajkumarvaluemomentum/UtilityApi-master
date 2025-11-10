namespace UtilityApi.Models
{
    public class ErrorRecord
    {
        // The Excel file name uploaded by user
        public string FileName { get; set; } = string.Empty;

        // The table where error occurred (e.g., Customers, Products, Sales)
        public string TableName { get; set; } = string.Empty;

        // JSON string that contains all error details for that upload
        public string ErrorDetails { get; set; } = string.Empty;

        // The date and time when the error was logged
        public DateTime LoggedDate { get; set; } = DateTime.UtcNow;
    }
}
