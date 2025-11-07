namespace UtilityApi;

public class Sale
{
    public string SaleId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public int? Quantity { get; set; }
    public decimal? Total { get; set; }
}
