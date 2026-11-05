namespace BTLwebdaugia.ViewModels;

public class ResubmitListingViewModel : CreateProductViewModel
{
    public long ProductId { get; set; }
    public long AuctionId { get; set; }
    public string Revision { get; set; } = "";
    public bool CanEditProduct { get; set; }
    public List<string> ExistingImages { get; set; } = new();
    public string? ProductReason { get; set; }
    public string? AuctionReason { get; set; }
}
