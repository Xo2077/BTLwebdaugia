
using BTLwebdaugia.Data;
using BTLwebdaugia.Models;
using BTLwebdaugia.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BTLwebdaugia.Controllers
{
    [Authorize]
    public class ProductController : Controller
    {
        private readonly BidHubDbContext _context;

        public ProductController(BidHubDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadCategoriesAsync();

            return View(
                "~/Views/Selling/Create.cshtml",
                new CreateProductViewModel()
            );
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            CreateProductViewModel model)
        {
            await LoadCategoriesAsync();

            if (!long.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out long sellerId))
            {
                return Challenge();
            }

            string name = model.Name?.Trim() ?? "";
            string description = model.Description?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(name) ||
                name.Length > 200)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    "Product name must contain 1–200 characters.");
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                ModelState.AddModelError(
                    nameof(model.Description),
                    "Description is required.");
            }

            bool categoryExists =
                await _context.Categories.AnyAsync(
                    c => c.CategoryId == model.CategoryId);

            if (!categoryExists)
            {
                ModelState.AddModelError(
                    nameof(model.CategoryId),
                    "Please select a valid category.");
            }

            if (model.StartingPrice < 50 ||
                model.StartingPrice !=
                    decimal.Truncate(model.StartingPrice))
            {
                ModelState.AddModelError(
                    nameof(model.StartingPrice),
                    "Starting price must be a whole number of at least 50.");
            }

            if (model.BidStep < 1 ||
                model.BidStep != decimal.Truncate(model.BidStep))
            {
                ModelState.AddModelError(
                    nameof(model.BidStep),
                    "Bid step must be a positive whole number.");
            }

            DateTime now = DateTime.UtcNow;
            DateTime startAt = now;

            if (model.StartMode == "Scheduled")
            {
                if (!model.ScheduledStartAt.HasValue)
                {
                    ModelState.AddModelError(
                        nameof(model.ScheduledStartAt),
                        "Scheduled start time is required.");
                }
                else
                {
                    // Form must explicitly request UTC input.
                    startAt = DateTime.SpecifyKind(
                        model.ScheduledStartAt.Value,
                        DateTimeKind.Utc);

                    if (startAt <= now)
                    {
                        ModelState.AddModelError(
                            nameof(model.ScheduledStartAt),
                            "Start time must be in the future.");
                    }
                }
            }
            else if (model.StartMode != "AfterApproval")
            {
                ModelState.AddModelError(
                    nameof(model.StartMode),
                    "Invalid start mode.");
            }

            DateTime endAt = startAt;

            if (model.EndTimeMode == "Duration")
            {
                if (!model.DurationMinutes.HasValue ||
                    model.DurationMinutes.Value < 60 ||
                    model.DurationMinutes.Value > 43200)
                {
                    ModelState.AddModelError(
                        nameof(model.DurationMinutes),
                        "Duration must be between 60 and 43200 minutes.");
                }
                else
                {
                    endAt = startAt.AddMinutes(
                        model.DurationMinutes.Value);
                }
            }
            else if (model.EndTimeMode == "Specific")
            {
                if (!model.SpecificEndAt.HasValue)
                {
                    ModelState.AddModelError(
                        nameof(model.SpecificEndAt),
                        "End time is required.");
                }
                else
                {
                    endAt = DateTime.SpecifyKind(
                        model.SpecificEndAt.Value,
                        DateTimeKind.Utc);

                    if (endAt <= startAt)
                    {
                        ModelState.AddModelError(
                            nameof(model.SpecificEndAt),
                            "End time must be after start time.");
                    }
                }
            }
            else
            {
                ModelState.AddModelError(
                    nameof(model.EndTimeMode),
                    "Invalid end time mode.");
            }

            // Image processing is deferred to the upload step.
            // Never silently discard submitted files.
            if (model.Images?.Count > 0)
            {
                ModelState.AddModelError(
                    nameof(model.Images),
                    "Image uploading is not enabled yet.");
            }

            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/Selling/Create.cshtml",
                    model);
            }

            var product = new Product
            {
                SellerId = sellerId,
                CategoryId = model.CategoryId,
                Name = name,
                Description = description,
                ApprovalStatus = "Pending",
                HasBeenApproved = false,
                IsDeleted = false,
                CreatedAt = now
            };

            var auction = new Auction
            {
                Product = product,
                StartingPrice = model.StartingPrice,
                BidStep = model.BidStep,
                EndTimeMode = model.EndTimeMode,
                DurationMinutes =
                    model.EndTimeMode == "Duration"
                        ? model.DurationMinutes
                        : null,
                StartAt = startAt,
                OriginalEndAt = endAt,
                CurrentEndAt = endAt,
                ExtendedOnce = false,
                ApprovalStatus = "Pending",
                CreatedAt = now
            };

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            _context.Products.Add(product);
            _context.Auctions.Add(auction);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                "Your listing has been submitted for review.";

            return RedirectToAction("Index", "Selling");
        }

        private async Task LoadCategoriesAsync()
        {
            ViewBag.Categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CategoryViewModel
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    Description = c.Description
                })
                .ToListAsync();
        }
    }
}
