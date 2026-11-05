using BTLwebdaugia.Models;

namespace BTLwebdaugia.Services;

// Create snapshots here; the caller saves them in the business transaction.
public static class OrderNotifications
{
    public static Notification[] AuctionSold(Auction auction, long winnerId, DateTime now)
    {
        return new[]
        {
            Create(auction, winnerId, "AuctionWon", now),
            Create(auction, auction.Product.SellerId, "AuctionSold", now)
        };
    }

    public static Notification[] Paid(Auction auction, long winnerId, DateTime now)
    {
        return new[]
        {
            Create(auction, winnerId, "PaymentSent", now),
            Create(auction, auction.Product.SellerId, "PaymentReceived", now)
        };
    }

    public static Notification Shipped(Auction auction, long winnerId, DateTime now)
    {
        return Create(auction, winnerId, "OrderShipped", now);
    }

    public static Notification[] Completed(Auction auction, long winnerId, DateTime now)
    {
        return new[]
        {
            Create(auction, winnerId, "OrderCompleted", now),
            Create(auction, auction.Product.SellerId, "OrderCompleted", now)
        };
    }

    public static bool IsOrderEvent(string eventType)
    {
        return eventType is "AuctionWon" or "AuctionSold" or "PaymentSent"
            or "PaymentReceived" or "OrderShipped" or "OrderCompleted";
    }

    private static Notification Create(Auction auction, long userId, string eventType, DateTime now)
    {
        return new Notification
        {
            UserId = userId,
            ProductId = auction.ProductId,
            AuctionId = auction.AuctionId,
            ProductName = auction.Product.Name,
            EventType = eventType,
            CreatedAt = now
        };
    }
}
