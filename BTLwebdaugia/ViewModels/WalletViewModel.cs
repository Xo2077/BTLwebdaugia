
namespace BTLwebdaugia.ViewModels;

public class WalletViewModel
{
    public string Username { get; set; } = string.Empty;

    public DateTime MemberSince { get; set; }

    public decimal Balance { get; set; }

    public List<WalletTransactionItemViewModel> Transactions { get; set; } = new();
}
