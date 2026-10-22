
using BTLwebdaugia.Data;
using BTLwebdaugia.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BTLwebdaugia.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly BidHubDbContext _context;

        public AdminController(BidHubDbContext context)
        {
            _context = context;
        }

        // =====================================
        // DASHBOARD
        // =====================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            DateTime now = DateTime.UtcNow;

            var model = new AdminDashboardViewModel
            {
                TotalUsers = await _context.Users
                    .AsNoTracking()
                    .CountAsync(),

                TotalProducts = await _context.Products
                    .AsNoTracking()
                    .CountAsync(p => !p.IsDeleted),

                PendingProducts = await _context.Products
                    .AsNoTracking()
                    .CountAsync(p =>
                        !p.IsDeleted &&
                        p.ApprovalStatus == "Pending"),

                LiveAuctions = await _context.Auctions
                    .AsNoTracking()
                    .CountAsync(a =>
                        a.ApprovalStatus == "Approved" &&
                        a.Product.ApprovalStatus == "Approved" &&
                        !a.Product.IsDeleted &&
                        a.StartAt <= now &&
                        a.CurrentEndAt > now &&
                        a.Result == null),

                TotalOrders = await _context.Orders
                    .AsNoTracking()
                    .CountAsync()
            };

            return View(model);
        }

        // =====================================
        // USERS
        // =====================================

        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users
                .AsNoTracking()
                .OrderByDescending(u => u.UserId)
                .Take(200)
                .ToListAsync();

            return View(users);
        }

        // =====================================
        // CATEGORIES
        // =====================================

        [HttpGet]
        public async Task<IActionResult> Categories()
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.CategoryId)
                .ToListAsync();

            return View(categories);
        }

        // =====================================
        // PRODUCTS
        // =====================================

        [HttpGet]
        public async Task<IActionResult> Products()
        {
            var products = await _context.Products
                .AsNoTracking()
                .Include(p => p.Seller)
                .Include(p => p.Category)
                .OrderByDescending(p => p.ProductId)
                .Take(200)
                .ToListAsync();

            return View(products);
        }

        // =====================================
        // AUCTIONS
        // =====================================

        [HttpGet]
        public async Task<IActionResult> Auctions()
        {
            var auctions = await _context.Auctions
                .AsNoTracking()
                .Include(a => a.Product)
                    .ThenInclude(p => p.Seller)
                .OrderByDescending(a => a.AuctionId)
                .Take(200)
                .ToListAsync();

            return View(auctions);
        }

        // =====================================
        // BIDS
        // =====================================

        [HttpGet]
        public async Task<IActionResult> Bids()
        {
            var bids = await _context.Bids
                .AsNoTracking()
                .Include(b => b.Bidder)
                .Include(b => b.AuctionNavigation)
                    .ThenInclude(a => a.Product)
                .OrderByDescending(b => b.PlacedAt)
                .ThenByDescending(b => b.BidId)
                .Take(200)
                .ToListAsync();

            return View(bids);
        }

        // =====================================
        // ORDERS
        // =====================================

        [HttpGet]
        public async Task<IActionResult> Orders()
        {
            var orders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Winner)
                .Include(o => o.Auction)
                    .ThenInclude(a => a.Product)
                .OrderByDescending(o => o.OrderId)
                .Take(200)
                .ToListAsync();

            return View(orders);
        }

        // =====================================
        // WALLETS
        // =====================================

        [HttpGet]
        public async Task<IActionResult> Wallets()
        {
            var wallets = await _context.Wallets
                .AsNoTracking()
                .Include(w => w.User)
                .OrderBy(w => w.WalletId)
                .Take(200)
                .ToListAsync();

            return View(wallets);
        }

        // =====================================
        // APPROVE PRODUCT
        // =====================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveProduct(long productId)
        {
            if (!TryGetAdminId(out long adminId))
                return Challenge();

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null || product.IsDeleted)
                return NotFound();

            if (product.ApprovalStatus != "Pending")
            {
                TempData["AdminError"] =
                    "Product is not pending.";

                return RedirectToAction(nameof(Products));
            }

            product.ApprovalStatus = "Approved";
            product.HasBeenApproved = true;
            product.RejectionReason = null;
            product.ReviewedBy = adminId;
            product.ReviewedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] =
                "Product approved successfully.";

            return RedirectToAction(nameof(Products));
        }

        // =====================================
        // REJECT PRODUCT
        // =====================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectProduct(
            long productId,
            string? reason)
        {
            if (!TryGetAdminId(out long adminId))
                return Challenge();

            reason = reason?.Trim();

            if (string.IsNullOrWhiteSpace(reason) ||
                reason.Length > 1000)
            {
                TempData["AdminError"] =
                    "Rejection reason is required (maximum 1000 characters).";

                return RedirectToAction(nameof(Products));
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null || product.IsDeleted)
                return NotFound();

            if (product.ApprovalStatus != "Pending")
            {
                TempData["AdminError"] =
                    "Product is not pending.";

                return RedirectToAction(nameof(Products));
            }

            product.ApprovalStatus = "Rejected";
            product.RejectionReason = reason;
            product.ReviewedBy = adminId;
            product.ReviewedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] =
                "Product rejected successfully.";

            return RedirectToAction(nameof(Products));
        }

        // =====================================
        // APPROVE AUCTION
        // =====================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAuction(long auctionId)
        {
            if (!TryGetAdminId(out long adminId))
                return Challenge();

            var auction = await _context.Auctions
                .Include(a => a.Product)
                .FirstOrDefaultAsync(a => a.AuctionId == auctionId);

            if (auction == null)
                return NotFound();

            // Phiên phải đang Pending.
            // Product phải được duyệt trước.
            if (auction.ApprovalStatus != "Pending" ||
                auction.Product.ApprovalStatus != "Approved" ||
                auction.Product.IsDeleted ||
                auction.Result != null)
            {
                TempData["AdminError"] =
                    "Auction or product is not eligible for approval.";

                return RedirectToAction(nameof(Auctions));
            }

            DateTime now = DateTime.UtcNow;

            // =====================================
            // MODE: DURATION
            // =====================================

            if (auction.EndTimeMode == "Duration")
            {
                if (!auction.DurationMinutes.HasValue ||
                    auction.DurationMinutes.Value < 60 ||
                    auction.DurationMinutes.Value > 43200)
                {
                    TempData["AdminError"] =
                        "Auction duration must be between 1 hour and 30 days.";

                    return RedirectToAction(nameof(Auctions));
                }

                // Nếu giờ bắt đầu đã qua:
                // Bắt đầu lại từ thời điểm Admin duyệt.
                if (auction.StartAt <= now)
                {
                    auction.StartAt = now;

                    auction.OriginalEndAt =
                        now.AddMinutes(
                            auction.DurationMinutes.Value);

                    auction.CurrentEndAt =
                        auction.OriginalEndAt;
                }
            }

            // =====================================
            // MODE: SPECIFIC
            // =====================================

            else if (auction.EndTimeMode == "Specific")
            {
                // Phiên có giờ kết thúc cố định.
                // Không tự kéo dài giờ kết thúc.

                if (auction.StartAt <= now)
                {
                    // Duyệt muộn:
                    // Chỉ chấp nhận nếu còn >= 1 giờ.
                    if (auction.CurrentEndAt < now.AddHours(1))
                    {
                        TempData["AdminError"] =
                            "Auction end time is too close or has expired. " +
                            "A new valid schedule is required.";

                        return RedirectToAction(nameof(Auctions));
                    }

                    auction.StartAt = now;
                }

                TimeSpan duration =
                    auction.CurrentEndAt - auction.StartAt;

                if (duration < TimeSpan.FromHours(1) ||
                    duration > TimeSpan.FromDays(30))
                {
                    TempData["AdminError"] =
                        "Auction duration must be between 1 hour and 30 days.";

                    return RedirectToAction(nameof(Auctions));
                }
            }
            else
            {
                TempData["AdminError"] =
                    "Invalid auction end time mode.";

                return RedirectToAction(nameof(Auctions));
            }

            // =====================================
            // FINAL SCHEDULE VALIDATION
            // =====================================

            if (auction.CurrentEndAt <= auction.StartAt ||
                auction.CurrentEndAt <= now)
            {
                TempData["AdminError"] =
                    "Auction schedule is invalid.";

                return RedirectToAction(nameof(Auctions));
            }

            // =====================================
            // UPDATE SQL SERVER
            // =====================================

            auction.ApprovalStatus = "Approved";
            auction.RejectionReason = null;
            auction.ReviewedBy = adminId;
            auction.ReviewedAt = now;

            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] =
                "Auction approved successfully.";

            return RedirectToAction(nameof(Auctions));
        }

        // =====================================
        // REJECT AUCTION
        // =====================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectAuction(
            long auctionId,
            string? reason)
        {
            if (!TryGetAdminId(out long adminId))
                return Challenge();

            reason = reason?.Trim();

            if (string.IsNullOrWhiteSpace(reason) ||
                reason.Length > 1000)
            {
                TempData["AdminError"] =
                    "Rejection reason is required (maximum 1000 characters).";

                return RedirectToAction(nameof(Auctions));
            }

            var auction = await _context.Auctions
                .FirstOrDefaultAsync(a => a.AuctionId == auctionId);

            if (auction == null)
                return NotFound();

            if (auction.ApprovalStatus != "Pending")
            {
                TempData["AdminError"] =
                    "Auction is not pending.";

                return RedirectToAction(nameof(Auctions));
            }

            auction.ApprovalStatus = "Rejected";
            auction.RejectionReason = reason;
            auction.ReviewedBy = adminId;
            auction.ReviewedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] =
                "Auction rejected successfully.";

            return RedirectToAction(nameof(Auctions));
        }

        // =====================================
        // HELPER
        // =====================================

        private bool TryGetAdminId(out long adminId)
        {
            return long.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out adminId);
        }
    }
}
