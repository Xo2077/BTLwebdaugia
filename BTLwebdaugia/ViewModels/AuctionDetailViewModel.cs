
namespace BTLwebdaugia.ViewModels;

public class AuctionDetailViewModel
{
    public long AuctionId { get; set; }

    public long ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string SellerName { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public List<string> ImagePaths { get; set; } = new();

    public decimal StartingPrice { get; set; }

    public decimal CurrentPrice { get; set; }

    public decimal BidStep { get; set; }

    public decimal NextMinimumBid { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime CurrentEndAt { get; set; }

    public string AuctionStatus { get; set; } = string.Empty;

    public List<BidHistoryItemViewModel> RecentBids { get; set; } = new();
}
