
namespace BTLwebdaugia.ViewModels;

public class ProductListItemViewModel
{
    public long ProductId { get; set; }

    public long AuctionId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string? ImagePath { get; set; }

    public decimal CurrentPrice { get; set; }

    public DateTime CurrentEndAt { get; set; }

    public string AuctionStatus { get; set; } = string.Empty;
}
