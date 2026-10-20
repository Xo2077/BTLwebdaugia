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
            var now = DateTime.UtcNow;

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

            var auctions = await _context.Auctions
                .AsNoTracking()
                .Where(a =>
                    a.ApprovalStatus == "Approved" &&
                    a.Product.ApprovalStatus == "Approved" &&
                    !a.Product.IsDeleted)
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

            ViewBag.Categories = categories;

            ViewBag.LiveAuctions = auctions
                .Where(a => a.AuctionStatus == "Live")
                .Take(3)
                .ToList();

            ViewBag.UpcomingAuctions = auctions
                .Where(a => a.AuctionStatus == "Upcoming")
                .Take(3)
                .ToList();

            ViewBag.EndedAuctions = auctions
                .Where(a => a.AuctionStatus == "Ended")
                .OrderByDescending(a => a.CurrentEndAt)
                .Take(3)
                .ToList();

            return View();
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

            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
