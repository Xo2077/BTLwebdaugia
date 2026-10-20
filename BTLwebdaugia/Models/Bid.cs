using System;
using System.Collections.Generic;

namespace BTLwebdaugia.Models;

public partial class Bid
{
    public long BidId { get; set; }

    public long AuctionId { get; set; }

    public long BidderId { get; set; }

    public decimal Amount { get; set; }

    public DateTime PlacedAt { get; set; }

    public virtual Auction? Auction { get; set; }

    public virtual Auction AuctionNavigation { get; set; } = null!;

    public virtual User Bidder { get; set; } = null!;
}
