namespace UtilityApi.Models
{
    public class ErrorRecord
    {
        public int Id { get; set; }
        public string? TableName { get; set; }
        public string? RecordIdentifier { get; set; }
        public int RowNumber { get; set; }
        public string? CustomerId { get; set; }
        public string? ProductId { get; set; }
        public string? SaleId { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
