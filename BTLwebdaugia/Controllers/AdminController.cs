using BTLwebdaugia.Data;
using BTLwebdaugia.Models;
using BTLwebdaugia.Services;
using BTLwebdaugia.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Security.Claims;

namespace BTLwebdaugia.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly BidHubDbContext _context;
        private readonly ILogger<AdminController> _logger;
        private readonly IStringLocalizer<SharedLabels> _labels;

        public AdminController(BidHubDbContext context, IStringLocalizer<SharedLabels> labels, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
            _labels = labels;
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
        public async Task<IActionResult> Users([FromQuery] AdminListFilterViewModel filter)
        {
            filter.Normalize();
            var query = _context.Users
                .AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                string term = filter.Search.Trim();
                query = query.Where(u => u.Username.Contains(term) || u.Email.Contains(term));
            }
            string[] statuses = ["User", "Admin"];
            if (!statuses.Contains(filter.Status))
                filter.Status = "";
            if (filter.Status != "")
                query = query.Where(u => u.Role == filter.Status);
            filter.TotalItems = await query.CountAsync();
            filter.Page = Math.Min(filter.Page, filter.TotalPages);
            ViewBag.AdminFilter = filter;
            ViewBag.AdminStatuses = statuses;
            ViewBag.AdminSearchPlaceholder = "Search by username or email";
            var items = await query.OrderByDescending(u => u.UserId)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> Categories([FromQuery] AdminListFilterViewModel filter)
        {
            filter.Normalize();
            var query = _context.Categories
                .AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                string term = filter.Search.Trim();
                query = query.Where(c => c.Name.Contains(term) || (c.Description != null && c.Description.Contains(term)));
            }
            string[] statuses = [];
            if (!statuses.Contains(filter.Status))
                filter.Status = "";
            filter.TotalItems = await query.CountAsync();
            filter.Page = Math.Min(filter.Page, filter.TotalPages);
            ViewBag.AdminFilter = filter;
            ViewBag.AdminStatuses = statuses;
            ViewBag.AdminSearchPlaceholder = "Search by category name or description";
            var items = await query.OrderByDescending(c => c.CategoryId)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> Products([FromQuery] AdminListFilterViewModel filter)
        {
            filter.Normalize();
            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Seller)
                .Include(p => p.Category).AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                string term = filter.Search.Trim();
                query = query.Where(p => p.Name.Contains(term) || p.Seller.Username.Contains(term) || p.Category.Name.Contains(term));
            }
            string[] statuses = ["Pending", "Approved", "Rejected", "Deleted"];
            if (!statuses.Contains(filter.Status))
                filter.Status = "";
            if (filter.Status == "Deleted")
                query = query.Where(p => p.IsDeleted);
            else if (filter.Status != "")
                query = query.Where(p => !p.IsDeleted && p.ApprovalStatus == filter.Status);
            filter.TotalItems = await query.CountAsync();
            filter.Page = Math.Min(filter.Page, filter.TotalPages);
            ViewBag.AdminFilter = filter;
            ViewBag.AdminStatuses = statuses;
            ViewBag.AdminSearchPlaceholder = "Search by product, seller or category";
            var items = await query.OrderByDescending(p => p.ProductId)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> Auctions([FromQuery] AdminListFilterViewModel filter)
        {
            filter.Normalize();
            var query = _context.Auctions
                .AsNoTracking()
                .Include(a => a.Product)
                    .ThenInclude(p => p.Seller).AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                string term = filter.Search.Trim();
                query = query.Where(a => a.Product.Name.Contains(term) || a.Product.Seller.Username.Contains(term));
            }
            string[] statuses = ["Pending", "Approved", "Rejected", "Upcoming", "Live", "Ended", "Sold", "Unsold"];
            if (!statuses.Contains(filter.Status))
                filter.Status = "";
            var now = DateTime.UtcNow;
            query = filter.Status switch
            {
                "Pending" or "Approved" or "Rejected" => query.Where(a => a.ApprovalStatus == filter.Status),
                "Upcoming" => query.Where(a => a.ApprovalStatus == "Approved" && a.Product.ApprovalStatus == "Approved" && !a.Product.IsDeleted && a.StartAt > now),
                "Live" => query.Where(a => a.ApprovalStatus == "Approved" && a.Product.ApprovalStatus == "Approved" && !a.Product.IsDeleted && a.StartAt <= now && a.CurrentEndAt > now && a.Result == null),
                "Ended" => query.Where(a => a.CurrentEndAt <= now),
                "Sold" or "Unsold" => query.Where(a => a.Result == filter.Status),
                _ => query
            };
            filter.TotalItems = await query.CountAsync();
            filter.Page = Math.Min(filter.Page, filter.TotalPages);
            ViewBag.AdminFilter = filter;
            ViewBag.AdminStatuses = statuses;
            ViewBag.AdminSearchPlaceholder = "Search by product or seller";
            var items = await query.OrderByDescending(a => a.AuctionId)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> Bids([FromQuery] AdminListFilterViewModel filter)
        {
            filter.Normalize();
            var query = _context.Bids
                .AsNoTracking()
                .Include(b => b.Bidder)
                .Include(b => b.AuctionNavigation)
                    .ThenInclude(a => a.Product).AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                string term = filter.Search.Trim();
                query = query.Where(b => b.Bidder.Username.Contains(term) || b.AuctionNavigation.Product.Name.Contains(term));
            }
            string[] statuses = [];
            if (!statuses.Contains(filter.Status))
                filter.Status = "";
            filter.TotalItems = await query.CountAsync();
            filter.Page = Math.Min(filter.Page, filter.TotalPages);
            ViewBag.AdminFilter = filter;
            ViewBag.AdminStatuses = statuses;
            ViewBag.AdminSearchPlaceholder = "Search by product or bidder";
            var items = await query.OrderByDescending(b => b.BidId)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> Orders([FromQuery] AdminListFilterViewModel filter)
        {
            filter.Normalize();
            var query = _context.Orders
                .AsNoTracking()
                .Include(o => o.Winner)
                .Include(o => o.Auction)
                    .ThenInclude(a => a.Product).AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                string term = filter.Search.Trim();
                query = query.Where(o => o.Winner.Username.Contains(term) || o.Auction.Product.Name.Contains(term));
            }
            string[] statuses = ["PendingPayment", "Paid", "Shipping", "Completed"];
            if (!statuses.Contains(filter.Status))
                filter.Status = "";
            if (filter.Status != "")
                query = query.Where(o => o.Status == filter.Status);
            filter.TotalItems = await query.CountAsync();
            filter.Page = Math.Min(filter.Page, filter.TotalPages);
            ViewBag.AdminFilter = filter;
            ViewBag.AdminStatuses = statuses;
            ViewBag.AdminSearchPlaceholder = "Search by product or winner";
            var items = await query.OrderByDescending(o => o.OrderId)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> Wallets([FromQuery] AdminListFilterViewModel filter)
        {
            filter.Normalize();
            var query = _context.Wallets
                .AsNoTracking()
                .Include(w => w.User).AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                string term = filter.Search.Trim();
                query = query.Where(w => w.User.Username.Contains(term));
            }
            string[] statuses = ["WithBalance", "Empty"];
            if (!statuses.Contains(filter.Status))
                filter.Status = "";
            if (filter.Status == "WithBalance")
                query = query.Where(w => w.Balance > 0);
            else if (filter.Status == "Empty")
                query = query.Where(w => w.Balance == 0);
            filter.TotalItems = await query.CountAsync();
            filter.Page = Math.Min(filter.Page, filter.TotalPages);
            ViewBag.AdminFilter = filter;
            ViewBag.AdminStatuses = statuses;
            ViewBag.AdminSearchPlaceholder = "Search by wallet owner username";
            var items = await query.OrderByDescending(w => w.WalletId)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();
            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveProduct(long productId)
        {
            if (!TryGetAdminId(out long adminId))
                return Challenge();

            await using var transaction = await _context.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var product = await _context.Products
                .FromSqlInterpolated(
                    $@"SELECT * FROM dbo.Products WITH (UPDLOCK, HOLDLOCK)
                       WHERE ProductId = {productId}")
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null || product.IsDeleted)
                return NotFound();

            if (product.ApprovalStatus != "Pending")
            {
                TempData["AdminError"] =
                    _labels["Product is not pending."].Value;

                return FinishAdminAction(nameof(Products));
            }

            product.ApprovalStatus = "Approved";
            product.HasBeenApproved = true;
            product.RejectionReason = null;
            product.ReviewedBy = adminId;
            product.ReviewedAt = DateTime.UtcNow;

            _context.Notifications.Add(new Notification
            {
                UserId = product.SellerId,
                ProductId = product.ProductId,
                AuctionId = null,
                EventType = "ProductApproved",
                ProductName = product.Name,
                Reason = null,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["AdminSuccess"] =
                _labels["Product approved successfully."].Value;

            return FinishAdminAction(nameof(Products));
        }

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
                ModelState.AddModelError("reason", "Rejection reason is required (maximum 1000 characters).");
                TempData["AdminError"] =
                    _labels["Rejection reason is required (maximum 1000 characters)."].Value;

                return FinishAdminAction(nameof(Products));
            }

            await using var transaction = await _context.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var product = await _context.Products
                .FromSqlInterpolated(
                    $@"SELECT * FROM dbo.Products WITH (UPDLOCK, HOLDLOCK)
                       WHERE ProductId = {productId}")
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null || product.IsDeleted)
                return NotFound();

            if (product.ApprovalStatus != "Pending")
            {
                TempData["AdminError"] =
                    _labels["Product is not pending."].Value;

                return FinishAdminAction(nameof(Products));
            }

            product.ApprovalStatus = "Rejected";
            product.RejectionReason = reason;
            product.ReviewedBy = adminId;
            product.ReviewedAt = DateTime.UtcNow;

            _context.Notifications.Add(new Notification
            {
                UserId = product.SellerId,
                ProductId = product.ProductId,
                AuctionId = null,
                EventType = "ProductRejected",
                ProductName = product.Name,
                Reason = reason,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["AdminSuccess"] =
                _labels["Product rejected successfully."].Value;

            return FinishAdminAction(nameof(Products));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAuction(long auctionId)
        {
            if (!TryGetAdminId(out long adminId))
                return Challenge();

            await using var transaction = await _context.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var auction = await _context.Auctions
                .FromSqlInterpolated(
                    $@"SELECT * FROM dbo.Auctions WITH (UPDLOCK, HOLDLOCK)
                       WHERE AuctionId = {auctionId}")
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
                    _labels["Auction or product is not eligible for approval."].Value;

                return FinishAdminAction(nameof(Auctions));
            }

            DateTime now = DateTime.UtcNow;

            if (auction.EndTimeMode == AuctionScheduleRules.DurationMode)
            {
                if (!auction.DurationMinutes.HasValue ||
                    auction.DurationMinutes.Value < AuctionScheduleRules.MinDurationMinutes ||
                    auction.DurationMinutes.Value > AuctionScheduleRules.MaxDurationMinutes)
                {
                    TempData["AdminError"] =
                        _labels["Auction duration must be between 60 minutes and 30 days."].Value;

                    return FinishAdminAction(nameof(Auctions));
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

            else if (auction.EndTimeMode is AuctionScheduleRules.SpecificInputMode or AuctionScheduleRules.FixedEndTimeMode)
            {
                // Phiên có giờ kết thúc cố định.
                // Không tự kéo dài giờ kết thúc.

                if (auction.StartAt <= now)
                {
                    // Duyệt muộn:
                    // Chỉ chấp nhận nếu còn >= 1 giờ.
                    if (auction.CurrentEndAt < now.Add(AuctionScheduleRules.MinDuration))
                    {
                        TempData["AdminError"] =
                            _labels["Auction must have at least 60 minutes remaining."].Value;

                        return FinishAdminAction(nameof(Auctions));
                    }

                    auction.StartAt = now;
                }

                TimeSpan duration =
                    auction.CurrentEndAt - auction.StartAt;

                if (duration < AuctionScheduleRules.MinDuration ||
                    duration > AuctionScheduleRules.MaxDuration)
                {
                    TempData["AdminError"] =
                        _labels["Auction duration must be between 60 minutes and 30 days."].Value;

                    return FinishAdminAction(nameof(Auctions));
                }
            }
            else
            {
                TempData["AdminError"] =
                    _labels["Invalid auction end time mode."].Value;

                return FinishAdminAction(nameof(Auctions));
            }

            if (auction.CurrentEndAt <= auction.StartAt ||
                auction.CurrentEndAt <= now)
            {
                TempData["AdminError"] =
                    _labels["Auction schedule is invalid."].Value;

                return FinishAdminAction(nameof(Auctions));
            }

            auction.ApprovalStatus = "Approved";
            auction.RejectionReason = null;
            auction.ReviewedBy = adminId;
            auction.ReviewedAt = now;

            _context.Notifications.Add(new Notification
            {
                UserId = auction.Product.SellerId,
                ProductId = auction.Product.ProductId,
                AuctionId = auction.AuctionId,
                EventType = "AuctionApproved",
                ProductName = auction.Product.Name,
                Reason = null,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["AdminSuccess"] =
                _labels["Auction approved successfully."].Value;

            return FinishAdminAction(nameof(Auctions));
        }

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
                ModelState.AddModelError("reason", "Rejection reason is required (maximum 1000 characters).");
                TempData["AdminError"] =
                    _labels["Rejection reason is required (maximum 1000 characters)."].Value;

                return FinishAdminAction(nameof(Auctions));
            }

            await using var transaction = await _context.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var auction = await _context.Auctions
                .FromSqlInterpolated(
                    $@"SELECT * FROM dbo.Auctions WITH (UPDLOCK, HOLDLOCK)
                       WHERE AuctionId = {auctionId}")
                .Include(a => a.Product)
                .FirstOrDefaultAsync(a => a.AuctionId == auctionId);

            if (auction == null)
                return NotFound();

            if (auction.ApprovalStatus != "Pending")
            {
                TempData["AdminError"] =
                    _labels["Auction is not pending."].Value;

                return FinishAdminAction(nameof(Auctions));
            }

            auction.ApprovalStatus = "Rejected";
            auction.RejectionReason = reason;
            auction.ReviewedBy = adminId;
            auction.ReviewedAt = DateTime.UtcNow;

            _context.Notifications.Add(new Notification
            {
                UserId = auction.Product.SellerId,
                ProductId = auction.Product.ProductId,
                AuctionId = auction.AuctionId,
                EventType = "AuctionRejected",
                ProductName = auction.Product.Name,
                Reason = reason,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["AdminSuccess"] =
                _labels["Auction rejected successfully."].Value;

            return FinishAdminAction(nameof(Auctions));
        }

        private IActionResult FinishAdminAction(string action)
        {
            string? error = (TempData["AdminError"] ?? TempData["CategoryError"]) as string;
            string? success = (TempData["AdminSuccess"] ?? TempData["CategorySuccess"]) as string;
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var errors = ModelState.Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(x => x.Key, x => x.Value!.Errors
                        .Select(e => _labels[string.IsNullOrEmpty(e.ErrorMessage)
                            ? "Invalid value." : e.ErrorMessage].Value).ToArray());
                if (error != null || errors.Count > 0)
                    return BadRequest(new { message = error ?? _labels["Please check the form."].Value, errors });
                return Json(new { message = success });
            }

            string? returnUrl = Request.Form["returnUrl"].FirstOrDefault();
            if (Url.IsLocalUrl(returnUrl) &&
                string.Equals(returnUrl!.Split('?')[0], Url.Action(action), StringComparison.OrdinalIgnoreCase))
                return LocalRedirect(returnUrl);

            return RedirectToAction(action);
        }

        private bool TryGetAdminId(out long adminId)
        {
            return long.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out adminId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(
            CategoryInputViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["CategoryError"] =
                    _labels["Please enter a valid category name."].Value;
                return FinishAdminAction(nameof(Categories));
            }

            string name = model.Name.Trim();

            bool exists = await _context.Categories
                .AnyAsync(c => c.Name == name);

            if (exists)
            {
                ModelState.AddModelError(nameof(model.Name), "Category name already exists.");
                TempData["CategoryError"] =
                    _labels["Category name already exists."].Value;
                return FinishAdminAction(nameof(Categories));
            }

            _context.Categories.Add(new BTLwebdaugia.Models.Category
            {
                Name = name,
                Description = model.Description?.Trim()
            });

            try
            {
                await _context.SaveChangesAsync();
                TempData["CategorySuccess"] =
                    _labels["Category created successfully."].Value;
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Category write failed in {Action}", ControllerContext.ActionDescriptor.ActionName);
                TempData["CategoryError"] =
                    _labels["Unable to create category. Check for duplicates."].Value;
            }

            return FinishAdminAction(nameof(Categories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCategory(
            CategoryInputViewModel model)
        {
            if (!ModelState.IsValid || model.CategoryId <= 0)
            {
                TempData["CategoryError"] =
                    _labels["Invalid category information."].Value;
                return FinishAdminAction(nameof(Categories));
            }

            var category = await _context.Categories
                .FindAsync(model.CategoryId);

            if (category == null)
                return NotFound();

            string name = model.Name.Trim();

            bool exists = await _context.Categories
                .AnyAsync(c =>
                    c.CategoryId != model.CategoryId &&
                    c.Name == name);

            if (exists)
            {
                ModelState.AddModelError(nameof(model.Name), "Category name already exists.");
                TempData["CategoryError"] =
                    _labels["Category name already exists."].Value;
                return FinishAdminAction(nameof(Categories));
            }

            category.Name = name;
            category.Description = model.Description?.Trim();

            try
            {
                await _context.SaveChangesAsync();
                TempData["CategorySuccess"] =
                    _labels["Category updated successfully."].Value;
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Category write failed in {Action}", ControllerContext.ActionDescriptor.ActionName);
                TempData["CategoryError"] =
                    _labels["Unable to update category."].Value;
            }

            return FinishAdminAction(nameof(Categories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int categoryId)
        {
            var category = await _context.Categories
                .FindAsync(categoryId);

            if (category == null)
                return NotFound();

            // Bảo toàn quan hệ khóa ngoại với sản phẩm.
            bool hasProducts = await _context.Products
                .AnyAsync(p => p.CategoryId == categoryId);

            if (hasProducts)
            {
                TempData["CategoryError"] =
                    _labels["Cannot delete a category containing products."].Value;
                return FinishAdminAction(nameof(Categories));
            }

            _context.Categories.Remove(category);

            try
            {
                await _context.SaveChangesAsync();

                TempData["CategorySuccess"] =
                    _labels["Category deleted successfully."].Value;
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Category write failed in {Action}", ControllerContext.ActionDescriptor.ActionName);
                TempData["CategoryError"] =
                    _labels["Unable to delete this category."].Value;
            }

            return FinishAdminAction(nameof(Categories));
        }

    }
}
