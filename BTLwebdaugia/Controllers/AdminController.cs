using BTLwebdaugia.Data;
using BTLwebdaugia.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
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


        [HttpGet]
        public async Task<IActionResult> Categories()
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.CategoryId)
                .ToListAsync();

            return View(categories);
        }

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


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveProduct(long productId)
        {
            if (!long.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out long adminId))
                return Challenge();

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null || product.IsDeleted)
                return NotFound();

            if (product.ApprovalStatus != "Pending")
            {
                TempData["AdminError"] = "Product is not pending.";
                return RedirectToAction(nameof(Products));
            }

            product.ApprovalStatus = "Approved";
            product.HasBeenApproved = true;
            product.RejectionReason = null;
            product.ReviewedBy = adminId;
            product.ReviewedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Product approved.";
            return RedirectToAction(nameof(Products));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectProduct(
            long productId,
            [FromForm, Required, StringLength(1000)] string reason)
        {
            if (!long.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out long adminId))
                return Challenge();

            if (string.IsNullOrWhiteSpace(reason) ||
                reason.Trim().Length > 1000)
            {
                TempData["AdminError"] = "A rejection reason is required.";
                return RedirectToAction(nameof(Products));
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null || product.IsDeleted)
                return NotFound();

            if (product.ApprovalStatus != "Pending")
            {
                TempData["AdminError"] = "Product is not pending.";
                return RedirectToAction(nameof(Products));
            }

            product.ApprovalStatus = "Rejected";
            product.RejectionReason = reason.Trim();
            product.ReviewedBy = adminId;
            product.ReviewedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Product rejected.";
            return RedirectToAction(nameof(Products));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAuction(long auctionId)
        {
            if (!long.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out long adminId))
                return Challenge();

            var auction = await _context.Auctions
                .Include(a => a.Product)
                .FirstOrDefaultAsync(a => a.AuctionId == auctionId);

            if (auction == null)
                return NotFound();

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

            // A duration-based auction whose stored start is already
            // in the past is rebased to approval time.
            if (auction.EndTimeMode == "Duration" &&
                auction.StartAt <= now)
            {
                if (!auction.DurationMinutes.HasValue ||
                    auction.DurationMinutes.Value < 60 ||
                    auction.DurationMinutes.Value > 43200)
                {
                    TempData["AdminError"] = "Invalid auction duration.";
                    return RedirectToAction(nameof(Auctions));
                }

                auction.StartAt = now;
                auction.OriginalEndAt =
                    now.AddMinutes(auction.DurationMinutes.Value);
                auction.CurrentEndAt = auction.OriginalEndAt;
            }
            else if (auction.StartAt <= now ||
                     auction.CurrentEndAt <= auction.StartAt ||
                     auction.CurrentEndAt <= now)
            {
                TempData["AdminError"] =
                    "Auction schedule has expired or is invalid.";
                return RedirectToAction(nameof(Auctions));
            }

            auction.ApprovalStatus = "Approved";
            auction.RejectionReason = null;
            auction.ReviewedBy = adminId;
            auction.ReviewedAt = now;

            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Auction approved.";
            return RedirectToAction(nameof(Auctions));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectAuction(
            long auctionId,
            [FromForm, Required, StringLength(1000)] string reason)
        {
            if (!long.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out long adminId))
                return Challenge();

            if (string.IsNullOrWhiteSpace(reason) ||
                reason.Trim().Length > 1000)
            {
                TempData["AdminError"] = "A rejection reason is required.";
                return RedirectToAction(nameof(Auctions));
            }

            var auction = await _context.Auctions
                .FirstOrDefaultAsync(a => a.AuctionId == auctionId);

            if (auction == null)
                return NotFound();

            if (auction.ApprovalStatus != "Pending")
            {
                TempData["AdminError"] = "Auction is not pending.";
                return RedirectToAction(nameof(Auctions));
            }

            auction.ApprovalStatus = "Rejected";
            auction.RejectionReason = reason.Trim();
            auction.ReviewedBy = adminId;
            auction.ReviewedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Auction rejected.";
            return RedirectToAction(nameof(Auctions));
        }






    }
}
