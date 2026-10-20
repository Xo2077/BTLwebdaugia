using System;
using System.Collections.Generic;

namespace BTLwebdaugia.Models;

public partial class Auction
{
    public long AuctionId { get; set; }

    public long ProductId { get; set; }

    public decimal StartingPrice { get; set; }

    public decimal BidStep { get; set; }

    public string EndTimeMode { get; set; } = null!;

    public int? DurationMinutes { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime OriginalEndAt { get; set; }

    public DateTime CurrentEndAt { get; set; }

    public bool ExtendedOnce { get; set; }

    public string ApprovalStatus { get; set; } = null!;

    public string? RejectionReason { get; set; }

    public long? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? Result { get; set; }

    public long? WinnerBidId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Bid? Bid { get; set; }

    public virtual ICollection<Bid> Bids { get; set; } = new List<Bid>();

    public virtual Order? Order { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual User? ReviewedByNavigation { get; set; }
}
