using BTLwebdaugia.Data;
using BTLwebdaugia.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;

namespace BTLwebdaugia.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly BidHubDbContext _db;

        public OrdersController(BidHubDbContext db)
        {
            _db = db;
        }

        private long CurrentUserId =>
            long.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var id) ? id : 0;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            long userId = CurrentUserId;
            if (userId == 0) return Challenge();

            var orders = await _db.Orders
                .AsNoTracking()
                .Include(o => o.Auction)
                    .ThenInclude(a => a.Product)
                .Where(o =>
                    o.WinnerId == userId ||
                    o.Auction.Product.SellerId == userId)
                .OrderByDescending(o => o.OrderId)
                .ToListAsync();

            return View(orders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(long orderId)
        {
            long userId = CurrentUserId;
            if (userId == 0) return Challenge();

            await using var tx = await _db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable);

            var order = await _db.Orders
                .FromSqlInterpolated(
                    $@"SELECT * FROM dbo.Orders
                       WITH (UPDLOCK, HOLDLOCK)
                       WHERE OrderId = {orderId}")
                .SingleOrDefaultAsync();

            if (order == null || order.WinnerId != userId)
                return NotFound();

            if (order.Status != "PendingPayment")
            {
                TempData["OrderError"] =
                    "This order is not awaiting payment.";
                return RedirectToAction(nameof(Index));
            }

            var auction = await _db.Auctions
                .AsNoTracking()
                .Include(a => a.Product)
                .SingleOrDefaultAsync(
                    a => a.AuctionId == order.AuctionId);

            if (auction == null ||
                auction.Result != "Sold" ||
                auction.Product.SellerId == userId ||
                order.FinalAmount <= 0)
            {
                return BadRequest("Invalid order.");
            }

            long sellerId = auction.Product.SellerId;

            // Khóa hai ví trong cùng transaction.
            var wallets = await _db.Wallets
                .FromSqlInterpolated(
                    $@"SELECT * FROM dbo.Wallets
                       WITH (UPDLOCK, HOLDLOCK)
                       WHERE UserId = {userId}
                          OR UserId = {sellerId}")
                .OrderBy(w => w.WalletId)
                .ToListAsync();

            var buyerWallet = wallets
                .FirstOrDefault(w => w.UserId == userId);

            var sellerWallet = wallets
                .FirstOrDefault(w => w.UserId == sellerId);

            if (buyerWallet == null || sellerWallet == null)
            {
                TempData["OrderError"] =
                    "Buyer or seller wallet not found.";
                return RedirectToAction(nameof(Index));
            }

            if (buyerWallet.Balance < order.FinalAmount)
            {
                TempData["OrderError"] =
                    "Insufficient wallet balance.";
                return RedirectToAction(nameof(Index));
            }

            buyerWallet.Balance -= order.FinalAmount;
            sellerWallet.Balance += order.FinalAmount;

            var transferId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            _db.WalletTransactions.AddRange(
                new WalletTransaction
                {
                    WalletId = buyerWallet.WalletId,
                    OrderId = order.OrderId,
                    TransactionType = "PaymentDebit",
                    Amount = order.FinalAmount,
                    CreatedAt = now,
                    TransferGroupId = transferId
                },
                new WalletTransaction
                {
                    WalletId = sellerWallet.WalletId,
                    OrderId = order.OrderId,
                    TransactionType = "PaymentCredit",
                    Amount = order.FinalAmount,
                    CreatedAt = now,
                    TransferGroupId = transferId
                }
            );

            order.Status = "Paid";
            order.PaidAt = now;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["OrderSuccess"] =
                "Wallet payment completed successfully.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ship(long orderId)
        {
            long userId = CurrentUserId;
            if (userId == 0) return Challenge();

            await using var tx = await _db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable);

            var order = await _db.Orders
                .FromSqlInterpolated(
                    $@"SELECT * FROM dbo.Orders
                       WITH (UPDLOCK, HOLDLOCK)
                       WHERE OrderId = {orderId}")
                .Include(o => o.Auction)
                    .ThenInclude(a => a.Product)
                .SingleOrDefaultAsync();

            if (order == null ||
                order.Auction.Product.SellerId != userId)
                return NotFound();

            if (order.Status != "Paid")
                return BadRequest("Order is not paid.");

            order.Status = "Shipping";
            order.ShippedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["OrderSuccess"] =
                "Shipment confirmed.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReceived(
            long orderId)
        {
            long userId = CurrentUserId;
            if (userId == 0) return Challenge();

            await using var tx = await _db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable);

            var order = await _db.Orders
                .FromSqlInterpolated(
                    $@"SELECT * FROM dbo.Orders
                       WITH (UPDLOCK, HOLDLOCK)
                       WHERE OrderId = {orderId}")
                .SingleOrDefaultAsync();

            if (order == null || order.WinnerId != userId)
                return NotFound();

            if (order.Status != "Shipping")
                return BadRequest("Order is not shipping.");

            order.Status = "Completed";
            order.CompletedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["OrderSuccess"] =
                "Delivery completed.";

            return RedirectToAction(nameof(Index));
        }
    }
}
