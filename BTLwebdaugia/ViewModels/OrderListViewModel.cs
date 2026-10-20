
namespace BTLwebdaugia.ViewModels;

public class OrderListViewModel
{
    public string Username { get; set; } = string.Empty;

    public DateTime MemberSince { get; set; }

    public List<OrderViewModel> Orders { get; set; } = new();

    public int TotalOrders { get; set; }

    public int AwaitingPaymentCount { get; set; }

    public int PaidCount { get; set; }

    public int CompletedCount { get; set; }

    public string SelectedStatus { get; set; } = "All";
}
