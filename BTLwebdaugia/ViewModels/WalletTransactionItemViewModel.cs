
namespace BTLwebdaugia.ViewModels;

public class WalletTransactionItemViewModel
{
    public long TransactionId { get; set; }

    public string TransactionType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; }
}
