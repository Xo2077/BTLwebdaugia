using System;
using System.Collections.Generic;

namespace BTLwebdaugia.Models;

public partial class Wallet
{
    public long WalletId { get; set; }

    public long UserId { get; set; }

    public decimal Balance { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
}
