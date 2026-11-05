using System.Security.Claims;
using BTLwebdaugia.Data;
using BTLwebdaugia.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTLwebdaugia.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly BidHubDbContext _db;

    public NotificationsController(BidHubDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult List(int page = 1)
    {
        Response.Headers.CacheControl = "no-store";
        return ViewComponent("Notifications", new { page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(long id)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Challenge();

        var item = await _db.Notifications
            .SingleOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);
        if (item == null)
            return NotFound();

        string url;
        if (OrderNotifications.IsOrderEvent(item.EventType))
        {
            var orderId = await _db.Orders.AsNoTracking()
                .Where(o => o.AuctionId == item.AuctionId &&
                    (o.WinnerId == userId || o.Auction.Product.SellerId == userId))
                .Select(o => (long?)o.OrderId)
                .SingleOrDefaultAsync();
            if (!orderId.HasValue)
                return NotFound();

            url = Url.Action("Index", "Orders")! + "#order-" + orderId.Value;
        }
        else
        {
            url = Url.Action("Details", "Selling", new { id = item.ProductId })!;
            if (item.AuctionId.HasValue)
                url += "#auction-" + item.AuctionId.Value;
        }

        if (item.ReadAt == null)
        {
            item.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { url });

        return LocalRedirect(url);
    }
}
