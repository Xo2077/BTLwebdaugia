using BTLwebdaugia.Data;
using BTLwebdaugia.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace BTLwebdaugia.Controllers
{
    public class HomeController : Controller
    {
        private readonly BidHubDbContext _context;

        public HomeController(BidHubDbContext context)
        {
            _context = context;
        }



        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Lấy danh mục từ SQL Server.
            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CategoryViewModel
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    Description = c.Description
                })
                .ToListAsync();

            // Lấy các phiên đấu giá ban đầu.
            var auctions = await GetHomepageAuctionsAsync(null);

            ViewBag.Categories = categories;
            ViewBag.SelectedCategoryId = null;

            return View(auctions);
        }



        [HttpGet]
        public async Task<IActionResult> FilterAuctions(
            [FromQuery] long? categoryId)
        {
            // Không ghi Session nếu categoryId không hợp lệ.
            if (categoryId.HasValue && categoryId.Value < 1)
            {
                return BadRequest("Invalid category.");
            }

            var auctions = await GetHomepageAuctionsAsync(categoryId);

            // Session lưu danh mục được chọn gần nhất.
            if (categoryId.HasValue)
            {
                HttpContext.Session.SetInt32(
                    "LastHomepageCategoryId",
                    checked((int)categoryId.Value)
                );
            }
            else
            {
                // All categories: xóa bộ lọc đã lưu.
                HttpContext.Session.Remove(
                    "LastHomepageCategoryId"
                );
            }

            ViewBag.SelectedCategoryId = categoryId;

            return PartialView("AuctionSections", auctions);
        }



        private async Task<List<ProductListItemViewModel>>
            GetHomepageAuctionsAsync(long? categoryId)
        {
            var now = DateTime.UtcNow;

            var query = _context.Auctions
                .AsNoTracking()
                .Where(a =>
                    a.ApprovalStatus == "Approved" &&
                    a.Product.ApprovalStatus == "Approved" &&
                    !a.Product.IsDeleted);

            // Nếu người dùng chọn danh mục thì lọc.
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(a =>
                    a.Product.CategoryId == categoryId.Value);
            }

            var auctions = await query
                .OrderBy(a => a.CurrentEndAt)
                .Select(a => new ProductListItemViewModel
                {
                    ProductId = a.ProductId,
                    AuctionId = a.AuctionId,
                    Name = a.Product.Name,
                    CategoryName = a.Product.Category.Name,

                    ImagePath = a.Product.ProductImages
                        .OrderBy(i => i.SortOrder)
                        .Select(i => i.ImagePath)
                        .FirstOrDefault(),

                    CurrentPrice = a.Bids
                        .OrderByDescending(b => b.Amount)
                        .Select(b => (decimal?)b.Amount)
                        .FirstOrDefault() ?? a.StartingPrice,

                    CurrentEndAt = a.CurrentEndAt,

                    AuctionStatus =
                        a.StartAt > now ? "Upcoming" :
                        a.CurrentEndAt <= now ? "Ended" :
                        "Live"
                })
                .ToListAsync();

            return auctions;
        }

        [HttpGet]
        public async Task<IActionResult> Category(
            [FromQuery] ProductFilterViewModel filter)
        {
            filter ??= new ProductFilterViewModel();

            filter.Page = Math.Max(1, filter.Page);
            filter.PageSize = 6;

            var allowedStatuses = new[]
            {
                "All", "Live", "Upcoming", "Ended"
            };

            if (!allowedStatuses.Contains(filter.Status))
                filter.Status = "All";

            if (filter.SortBy != "EndingSoon" &&
                filter.SortBy != "Newest" &&
                filter.SortBy != "PriceLow" &&
                filter.SortBy != "PriceHigh")
            {
                filter.SortBy = "EndingSoon";
            }

            var now = DateTime.UtcNow;

            var query = _context.Auctions
                .AsNoTracking()
                .Where(a =>
                    a.ApprovalStatus == "Approved" &&
                    a.Product.ApprovalStatus == "Approved" &&
                    !a.Product.IsDeleted);

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                string term = filter.SearchTerm.Trim();

                query = query.Where(a =>
                    a.Product.Name.Contains(term));
            }

            if (filter.CategoryId.HasValue &&
                filter.CategoryId.Value > 0)
            {
                query = query.Where(a =>
                    a.Product.CategoryId == filter.CategoryId.Value);
            }

            query = filter.Status switch
            {
                "Live" => query.Where(a =>
                    a.StartAt <= now &&
                    a.CurrentEndAt > now),

                "Upcoming" => query.Where(a =>
                    a.StartAt > now),

                "Ended" => query.Where(a =>
                    a.CurrentEndAt <= now),

                _ => query
            };

            query = filter.SortBy switch
            {
                "Newest" => query
                    .OrderByDescending(a => a.CreatedAt)
                    .ThenByDescending(a => a.AuctionId),

                "PriceLow" => query
                    .OrderBy(a => a.StartingPrice)
                    .ThenBy(a => a.AuctionId),

                "PriceHigh" => query
                    .OrderByDescending(a => a.StartingPrice)
                    .ThenByDescending(a => a.AuctionId),

                _ => query
                    .OrderBy(a => a.CurrentEndAt)
                    .ThenBy(a => a.AuctionId)
            };

            int totalItems = await query.CountAsync();

            int totalPages = (int)Math.Ceiling(
                totalItems / (double)filter.PageSize);

            filter.Page = Math.Min(
                filter.Page,
                Math.Max(1, totalPages));

            var products = await query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(a => new ProductListItemViewModel
                {
                    ProductId = a.ProductId,
                    AuctionId = a.AuctionId,
                    Name = a.Product.Name,
                    CategoryName = a.Product.Category.Name,

                    ImagePath = a.Product.ProductImages
                        .OrderBy(i => i.SortOrder)
                        .Select(i => i.ImagePath)
                        .FirstOrDefault(),

                    CurrentPrice = a.Bids
                        .OrderByDescending(b => b.Amount)
                        .Select(b => (decimal?)b.Amount)
                        .FirstOrDefault() ?? a.StartingPrice,

                    CurrentEndAt = a.CurrentEndAt,

                    AuctionStatus =
                        a.StartAt > now ? "Upcoming" :
                        a.CurrentEndAt <= now ? "Ended" :
                        "Live"
                })
                .ToListAsync();

            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CategoryViewModel
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    Description = c.Description
                })
                .ToListAsync();

            var viewModel = new ProductListViewModel
            {
                Categories = categories,
                Products = products,
                Filter = filter,
                TotalItems = totalItems,
                TotalPages = totalPages
            };


            int? lastCategoryId = HttpContext.Session.GetInt32(
                "LastHomepageCategoryId"
            );

            ViewBag.LastHomepageCategoryName = categories
                .FirstOrDefault(c => c.CategoryId == lastCategoryId)
                ?.Name;


            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
