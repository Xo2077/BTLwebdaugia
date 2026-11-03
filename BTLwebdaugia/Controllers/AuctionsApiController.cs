using BTLwebdaugia.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTLwebdaugia.Controllers;

[ApiController]
[Route("api/auctions")]
public class AuctionsApiController : ControllerBase
{
    private readonly BidHubDbContext _db;

    public AuctionsApiController(BidHubDbContext db)
    {
        _db = db;
    }

    // GET /api/auctions?categoryId=2&page=1&pageSize=10
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? categoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (categoryId < 1 || page < 1 ||
            pageSize < 1 || pageSize > 50)
        {
            return BadRequest(new
            {
                message = "Invalid paging or category parameters."
            });
        }

        var now = DateTime.UtcNow;

        var query = _db.Auctions
            .AsNoTracking()
            .Where(a =>
                a.ApprovalStatus == "Approved" &&
                a.Product.ApprovalStatus == "Approved" &&
                !a.Product.IsDeleted);

        if (categoryId.HasValue)
        {
            query = query.Where(a =>
                a.Product.CategoryId == categoryId.Value);
        }

        int totalItems = await query.CountAsync();

        var items = await query
            .OrderBy(a => a.CurrentEndAt)
            .ThenBy(a => a.AuctionId)
            .Skip(((long)page - 1) * pageSize > int.MaxValue
                ? int.MaxValue
                : (page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.AuctionId,
                a.ProductId,
                ProductName = a.Product.Name,
                CategoryId = a.Product.CategoryId,
                CategoryName = a.Product.Category.Name,
                a.StartingPrice,
                a.CurrentEndAt,
                Status =
                    a.StartAt > now ? "Upcoming" :
                    a.CurrentEndAt <= now ? "Ended" :
                    "Live"
            })
            .ToListAsync();

        return Ok(new
        {
            page,
            pageSize,
            totalItems,
            items
        });
    }

    // GET /api/auctions/12
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var auction = await _db.Auctions
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
                CategoryName = a.Product.Category.Name,
                a.StartingPrice,
                a.StartAt,
                a.CurrentEndAt
            })
            .FirstOrDefaultAsync();

        if (auction == null)
        {
            return NotFound(new
            {
                message = "Auction not found."
            });
        }

        return Ok(auction);
    }
}
