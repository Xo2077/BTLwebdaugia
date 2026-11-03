
namespace BTLwebdaugia.ViewModels;

public class BidHistoryItemViewModel
{
    public long BidId { get; set; }

    public string BidderName { get; set; } = string.Empty;

    public string? AvatarPath { get; set; }

    public decimal Amount { get; set; }

    public DateTime PlacedAt { get; set; }
}
