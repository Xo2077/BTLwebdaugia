
using BTLwebdaugia.Data;
using BTLwebdaugia.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTLwebdaugia.Controllers
{
    public class AuctionController : Controller
    {
        private readonly BidHubDbContext _context;

        public AuctionController(BidHubDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Details(long id)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            var auction = await _context.Auctions
                .AsNoTracking()
                .Where(a =>
                    a.AuctionId == id &&
                    a.ApprovalStatus == "Approved" &&
                    a.Product.ApprovalStatus == "Approved" &&
                    !a.Product.IsDeleted)
                .Select(a => new
                {
                    a.AuctionId,
                    a.ProductId,
                    ProductName = a.Product.Name,
                    a.Product.Description,
                    SellerName = a.Product.Seller.Username,
                    CategoryName = a.Product.Category.Name,
                    a.StartingPrice,
                    a.BidStep,
                    a.StartAt,
                    a.CurrentEndAt,

                    ImagePaths = a.Product.ProductImages
                        .OrderBy(i => i.SortOrder)
                        .Select(i => i.ImagePath)
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (auction == null)
            {
                return NotFound();
            }

            var highestBid = await _context.Bids
                .AsNoTracking()
                .Where(b => b.AuctionId == id)
                .MaxAsync(b => (decimal?)b.Amount);

            decimal currentPrice =
                highestBid ?? auction.StartingPrice;

            decimal nextMinimumBid = highestBid.HasValue
                ? highestBid.Value + auction.BidStep
                : auction.StartingPrice;

            var recentBids = await _context.Bids
                .AsNoTracking()
                .Where(b => b.AuctionId == id)
                .OrderByDescending(b => b.Amount)
                .ThenBy(b => b.PlacedAt)
                .ThenBy(b => b.BidId)
                .Take(10)
                .Select(b => new BidHistoryItemViewModel
                {
                    BidId = b.BidId,
                    BidderName = b.Bidder.Username,
                    Amount = b.Amount,
                    PlacedAt = b.PlacedAt
                })
                .ToListAsync();

            DateTime now = DateTime.UtcNow;

            string status;

            if (auction.StartAt > now)
            {
                status = "Upcoming";
            }
            else if (auction.CurrentEndAt <= now)
            {
                status = "Ended";
            }
            else
            {
                status = "Live";
            }

            var model = new AuctionDetailViewModel
            {
                AuctionId = auction.AuctionId,
                ProductId = auction.ProductId,
                ProductName = auction.ProductName,
                Description = auction.Description,
                SellerName = auction.SellerName,
                CategoryName = auction.CategoryName,
                ImagePaths = auction.ImagePaths,
                StartingPrice = auction.StartingPrice,
                CurrentPrice = currentPrice,
                BidStep = auction.BidStep,
                NextMinimumBid = nextMinimumBid,
                StartAt = auction.StartAt,
                CurrentEndAt = auction.CurrentEndAt,
                AuctionStatus = status,
                RecentBids = recentBids
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult Ended()
        {
            return View();
        }
    }
}
