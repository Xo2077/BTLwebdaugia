using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BTLwebdaugia.Data;
using BTLwebdaugia.ViewModels;
using Microsoft.EntityFrameworkCore;


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





    }
}
