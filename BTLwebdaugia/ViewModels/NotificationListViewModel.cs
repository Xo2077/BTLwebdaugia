using BTLwebdaugia.Models;

namespace BTLwebdaugia.ViewModels;

public class NotificationListViewModel
{
    public bool Available { get; set; } = true;
    public List<Notification> Items { get; set; } = new();
    public int UnreadCount { get; set; }
    public int Page { get; set; }
    public bool HasNext { get; set; }
}
