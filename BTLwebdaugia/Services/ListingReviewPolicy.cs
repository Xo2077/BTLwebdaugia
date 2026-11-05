using BTLwebdaugia.Models;

namespace BTLwebdaugia.Services;

// Call with Auctions and their Bids and Order loaded.
public static class ListingReviewPolicy
{
    public static bool CanEditProduct(Product product)
    {
        return !product.HasBeenApproved && product.ApprovalStatus == "Rejected" &&
            product.Auctions.All(a => a.ApprovalStatus != "Approved" && IsUnused(a));
    }

    public static bool CanResubmit(Product product)
    {
        var latest = product.Auctions.OrderByDescending(a => a.AuctionId).FirstOrDefault();
        if (product.IsDeleted || latest == null || !IsUnused(latest))
            return false;

        return (CanEditProduct(product) && latest.ApprovalStatus is "Pending" or "Rejected") ||
            (product.ApprovalStatus == "Approved" && latest.ApprovalStatus == "Rejected");
    }

    public static string Revision(Product product, Auction auction)
    {
        return $"{product.ReviewedAt?.Ticks}:{auction.AuctionId}:{auction.ReviewedAt?.Ticks}";
    }

    private static bool IsUnused(Auction auction)
    {
        return auction.Bids.Count == 0 && auction.Order == null &&
            auction.Result == null && auction.WinnerBidId == null;
    }
}
