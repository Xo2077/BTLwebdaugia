using System.Security.Claims;
using Microsoft.Data.SqlClient;
using BTLwebdaugia.Data;
using BTLwebdaugia.Models;
using BTLwebdaugia.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTLwebdaugia.ViewComponents;

public class NotificationsViewComponent : ViewComponent
{
    private readonly BidHubDbContext _db;
    private readonly ILogger<NotificationsViewComponent> _logger;

    public NotificationsViewComponent(BidHubDbContext db, ILogger<NotificationsViewComponent> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync(int page = 1)
    {
        if (!long.TryParse(HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Content("");

        var query = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        int count;
        try
        {
            count = await query.CountAsync();
        }
        catch (SqlException ex) when (ex.Number == 208 &&
            ex.Message.Contains("Notifications", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Notifications table is missing. Apply the database upgrade script.");
            return View(new NotificationListViewModel { Available = false });
        }
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(count / 10.0)));
        var model = new NotificationListViewModel
        {
            UnreadCount = await query.CountAsync(n => n.ReadAt == null),
            Page = page,
            HasNext = page * 10L < count,
            Items = await query.OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.NotificationId)
                .Skip((page - 1) * 10).Take(10).ToListAsync()
        };
        return View(model);
    }
}
