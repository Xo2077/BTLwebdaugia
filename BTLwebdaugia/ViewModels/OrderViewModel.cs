
namespace BTLwebdaugia.ViewModels;

public class OrderViewModel
{
    public long OrderId { get; set; }

    public long AuctionId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public decimal FinalAmount { get; set; }

    public DateTime OrderDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime? PaidAt { get; set; }

    public DateTime? ShippedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
