using System;
using System.Collections.Generic;

namespace BTLwebdaugia.Models;

public partial class WalletTransaction
{
    public long TransactionId { get; set; }

    public long WalletId { get; set; }

    public long? OrderId { get; set; }

    public string TransactionType { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? TransferGroupId { get; set; }

    public Guid? RequestId { get; set; }

    public virtual Order? Order { get; set; }

    public virtual Wallet Wallet { get; set; } = null!;
}
