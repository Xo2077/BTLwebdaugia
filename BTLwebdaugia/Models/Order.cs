using System;
using System.Collections.Generic;

namespace BTLwebdaugia.Models;

public partial class Order
{
    public long OrderId { get; set; }

    public long AuctionId { get; set; }

    public long WinnerId { get; set; }

    public decimal FinalAmount { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? PaidAt { get; set; }

    public DateTime? ShippedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual Auction Auction { get; set; } = null!;

    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();

    public virtual User Winner { get; set; } = null!;
}
