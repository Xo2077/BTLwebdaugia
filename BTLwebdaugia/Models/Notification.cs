namespace BTLwebdaugia.Models;

public class Notification
{
    public long NotificationId { get; set; }
    public long UserId { get; set; }
    public long ProductId { get; set; }
    public long? AuctionId { get; set; }
    public string EventType { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
