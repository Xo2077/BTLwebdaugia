
namespace BTLwebdaugia.ViewModels;

public class AdminPendingProductViewModel
{
    public long ProductId { get; set; }

    public long? AuctionId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string SellerName { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string? ImagePath { get; set; }

    public decimal? StartingPrice { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public string ApprovalStatus { get; set; } = string.Empty;
}
