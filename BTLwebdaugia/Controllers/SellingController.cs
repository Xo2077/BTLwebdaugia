
using BTLwebdaugia.Data;
using BTLwebdaugia.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BTLwebdaugia.Controllers
{
    [Authorize]
    public class SellingController : Controller
    {
        private readonly BidHubDbContext _context;

        public SellingController(BidHubDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!long.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out long sellerId))
            {
                return Challenge();
            }

            List<Product> listings = await _context.Products
                .AsNoTracking()
                .Where(p =>
                    p.SellerId == sellerId &&
                    !p.IsDeleted)
                .Include(p => p.Auctions)
                    .ThenInclude(a => a.Bids)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(listings);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return RedirectToAction("Create", "Product");
        }
    }
}
