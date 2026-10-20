
using BTLwebdaugia.Data;
using BTLwebdaugia.Models;
using BTLwebdaugia.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;

namespace BTLwebdaugia.Controllers
{
    [Authorize]
    public class BiddingController : Controller
    {
        private readonly BidHubDbContext _context;

        public BiddingController(BidHubDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceBid(
            PlaceBidViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["BidError"] =
                    "Invalid bid information.";

                return RedirectToAuction(model.AuctionId);
            }

            if (!long.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out long bidderId))
            {
                return Challenge();
            }

            if (model.Amount != decimal.Truncate(model.Amount) ||
                model.Amount < 1 ||
                model.Amount > 999999999999999999m)
            {
                TempData["BidError"] =
                    "Bid amount must be a valid whole number.";

                return RedirectToAuction(model.AuctionId);
            }

            try
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                // UPDLOCK serializes bidding on the same auction.
                // HOLDLOCK retains the lock until transaction end.
                var auction = await _context.Auctions
                    .FromSqlInterpolated(
                        $@"SELECT * FROM dbo.Auctions
                           WITH (UPDLOCK, HOLDLOCK)
                           WHERE AuctionId = {model.AuctionId}")
                    .Include(a => a.Product)
                    .SingleOrDefaultAsync();

                if (auction == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound();
                }

                if (auction.ApprovalStatus != "Approved" ||
                    auction.Product.ApprovalStatus != "Approved" ||
                    auction.Product.IsDeleted)
                {
                    await transaction.RollbackAsync();
                    return NotFound();
                }

                if (auction.Product.SellerId == bidderId)
                {
                    TempData["BidError"] =
                        "You cannot bid on your own product.";

                    await transaction.RollbackAsync();

                    return RedirectToAuction(model.AuctionId);
                }

                DateTime now = DateTime.UtcNow;

                if (now < auction.StartAt ||
                    now >= auction.CurrentEndAt)
                {
                    TempData["BidError"] =
                        "This auction is not currently active.";

                    await transaction.RollbackAsync();

                    return RedirectToAuction(model.AuctionId);
                }

                if (auction.Result != null)
                {
                    TempData["BidError"] =
                        "This auction has already been finalized.";

                    await transaction.RollbackAsync();

                    return RedirectToAuction(model.AuctionId);
                }

                decimal? highestBid = await _context.Bids
                    .Where(b => b.AuctionId == model.AuctionId)
                    .MaxAsync(b => (decimal?)b.Amount);

                decimal minimumBid;

                if (highestBid.HasValue)
                {
                    minimumBid = highestBid.Value + auction.BidStep;
                }
                else
                {
                    minimumBid = auction.StartingPrice;
                }

                if (model.Amount < minimumBid)
                {
                    TempData["BidError"] =
                        $"Minimum bid is ${minimumBid:N0}.";

                    await transaction.RollbackAsync();

                    return RedirectToAuction(model.AuctionId);
                }

                var bid = new Bid
                {
                    AuctionId = model.AuctionId,
                    BidderId = bidderId,
                    Amount = model.Amount,
                    PlacedAt = DateTime.UtcNow
                };

                _context.Bids.Add(bid);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["BidSuccess"] =
                    $"Your bid of ${model.Amount:N0} was placed successfully.";

                return RedirectToAuction(model.AuctionId);
            }
            catch (SqlException ex)
                when (ex.Number == 1205 ||
                      ex.Number == 1222 ||
                      ex.Number == 3960)
            {
                TempData["BidError"] =
                    "Another transaction affected this auction. Please try again.";

                return RedirectToAuction(model.AuctionId);
            }
        }

        private IActionResult RedirectToAuction(long auctionId)
        {
            if (auctionId <= 0)
            {
                return RedirectToAction("Category", "Home");
            }

            return RedirectToAction(
                "Details",
                "Auction",
                new { id = auctionId });
        }
    }
}
