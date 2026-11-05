using BTLwebdaugia.Data;
using BTLwebdaugia.Models;
using BTLwebdaugia.Services;
using BTLwebdaugia.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System;

namespace BTLwebdaugia.Controllers
{
    [Authorize]
    public partial class ProductController : Controller
    {
        private readonly BidHubDbContext _context;
        private readonly ILogger<ProductController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IStringLocalizer<SharedLabels> _labels;

        private const int MaxImageCount = 5;
        private const long MaxImageBytes = 5 * 1024 * 1024;

        public ProductController(
            BidHubDbContext context,
            IWebHostEnvironment environment,
            IStringLocalizer<SharedLabels> labels,
            ILogger<ProductController> logger)
        {
            _context = context;
            _logger = logger;
            _environment = environment;
            _labels = labels;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadCategoriesAsync();

            return View(
                "~/Views/Selling/Create.cshtml",
                new CreateProductViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(30 * 1024 * 1024)]
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

            var (startAt, endAt, images) = await ValidateListingAsync(model);
            string name = model.Name?.Trim() ?? "";
            string description = model.Description?.Trim() ?? "";
            DateTime now = DateTime.UtcNow;

            if (!ModelState.IsValid)
            {
                LocalizeListingErrors();
                return View(
                    "~/Views/Selling/Create.cshtml",
                    model);
            }

            // Product remains pending until Admin approval.
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
                EndTimeMode = model.EndTimeMode == AuctionScheduleRules.DurationMode ? AuctionScheduleRules.DurationMode : AuctionScheduleRules.FixedEndTimeMode,
                DurationMinutes =
                    model.EndTimeMode == AuctionScheduleRules.DurationMode
                        ? model.DurationMinutes
                        : null,
                StartAt = startAt,
                OriginalEndAt = endAt,
                CurrentEndAt = endAt,
                ExtendedOnce = false,
                ApprovalStatus = "Pending",
                CreatedAt = now
            };

            var uploadedFiles = new List<string>();

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                _context.Products.Add(product);
                _context.Auctions.Add(auction);

                // Save first to obtain ProductId.
                await _context.SaveChangesAsync();

                await SaveProductImagesAsync(product.ProductId, images, uploadedFiles);

                // Save ProductImages into SQL Server.
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();

                DeleteListingFiles(product.ProductId, uploadedFiles, physicalPaths: true);

                throw;
            }

            TempData["SuccessMessage"] =
                "Your listing has been submitted for review.";

            return RedirectToAction(
                "Index",
                "Selling");
        }

        private static async Task<bool> IsValidImageAsync(
            IFormFile file)
        {
            string extension = Path.GetExtension(
                file.FileName).ToLowerInvariant();

            string expectedType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".avif" => "image/avif",
                _ => ""
            };

            if (string.IsNullOrEmpty(expectedType) ||
                !string.Equals(
                    file.ContentType,
                    expectedType,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            byte[] header = new byte[32];

            await using var stream = file.OpenReadStream();

            int totalRead = 0;

            while (totalRead < header.Length)
            {
                int count = await stream.ReadAsync(
                    header.AsMemory(totalRead));

                if (count == 0)
                    break;

                totalRead += count;
            }

            bool jpeg = totalRead >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF;

            bool png = totalRead >= 8 &&
                header.AsSpan(0, 8).SequenceEqual(
                    new byte[]
                    {
                0x89, 0x50, 0x4E, 0x47,
                0x0D, 0x0A, 0x1A, 0x0A
                    });

            bool webp = totalRead >= 12 &&
                header[0] == 'R' &&
                header[1] == 'I' &&
                header[2] == 'F' &&
                header[3] == 'F' &&
                header[8] == 'W' &&
                header[9] == 'E' &&
                header[10] == 'B' &&
                header[11] == 'P';

            bool gif = totalRead >= 6 &&
                header[0] == 'G' &&
                header[1] == 'I' &&
                header[2] == 'F' &&
                header[3] == '8' &&
                (header[4] == '7' || header[4] == '9') &&
                header[5] == 'a';

            bool bmp = totalRead >= 2 &&
                header[0] == 'B' &&
                header[1] == 'M';

            bool avif = totalRead >= 16 &&
                header[4] == 'f' &&
                header[5] == 't' &&
                header[6] == 'y' &&
                header[7] == 'p' &&
                (
                    // AVIF or AVIS major brand.
                    (
                        header[8] == 'a' &&
                        header[9] == 'v' &&
                        header[10] == 'i' &&
                        (header[11] == 'f' || header[11] == 's')
                    )
                    ||
                    // AVIF compatible brand at first slot.
                    (
                        header[8] == 'm' &&
                        header[9] == 'i' &&
                        header[10] == 'f' &&
                        header[11] == '1' &&
                        header[12] == 'a' &&
                        header[13] == 'v' &&
                        header[14] == 'i' &&
                        header[15] == 'f'
                    )
                );

            return expectedType switch
            {
                "image/jpeg" => jpeg,
                "image/png" => png,
                "image/webp" => webp,
                "image/gif" => gif,
                "image/bmp" => bmp,
                "image/avif" => avif,
                _ => false
            };
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
