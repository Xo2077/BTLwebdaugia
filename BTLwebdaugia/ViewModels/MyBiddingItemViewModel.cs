
namespace BTLwebdaugia.ViewModels
{
    public class MyBiddingItemViewModel
    {
        public long AuctionId { get; set; }

        public string ProductName { get; set; } = "";

        public decimal MyHighestBid { get; set; }

        public decimal CurrentHighestBid { get; set; }

        public DateTime CurrentEndAt { get; set; }

        public string Status { get; set; } = "";
    }
}
