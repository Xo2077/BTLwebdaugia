
using System.ComponentModel.DataAnnotations;

namespace BTLwebdaugia.ViewModels;

public class PlaceBidViewModel
{
    [Range(1, long.MaxValue,
        ErrorMessage = "Invalid auction.")]
    public long AuctionId { get; set; }

    [Range(typeof(decimal), "1", "999999999999999999",
        ErrorMessage = "Bid amount must be greater than zero.")]
    public decimal Amount { get; set; }
}
