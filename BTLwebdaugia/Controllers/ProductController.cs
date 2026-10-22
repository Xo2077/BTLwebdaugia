
using BTLwebdaugia.Data;
using BTLwebdaugia.Models;
using BTLwebdaugia.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;

namespace BTLwebdaugia.Controllers
{
    [Authorize]
    public class ProductController : Controller
    {
        private readonly BidHubDbContext _context;
        private readonly IWebHostEnvironment _environment;

        private const int MaxImageCount = 5;
        private const long MaxImageBytes = 5 * 1024 * 1024;

        public ProductController(
            BidHubDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
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

            string name = model.Name?.Trim() ?? "";
            string description = model.Description?.Trim() ?? "";

            // Validate product information.
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

            bool categoryExists = await _context.Categories
                .AnyAsync(c => c.CategoryId == model.CategoryId);

            if (!categoryExists)
            {
                ModelState.AddModelError(
                    nameof(model.CategoryId),
                    "Please select a valid category.");
            }

            // USD whole-number rules.
            if (model.StartingPrice < 50 ||
                model.StartingPrice !=
                    decimal.Truncate(model.StartingPrice))
            {
                ModelState.AddModelError(
                    nameof(model.StartingPrice),
                    "Starting price must be a whole number of at least $50.");
            }

            if (model.BidStep < 1 ||
                model.BidStep !=
                    decimal.Truncate(model.BidStep))
            {
                ModelState.AddModelError(
                    nameof(model.BidStep),
                    "Bid step must be a positive whole number.");
            }

            // Validate auction schedule.
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

            // Validate uploaded images.
            var images = model.Images?
                .Where(f => f != null && f.Length > 0)
                .ToList() ?? new List<IFormFile>();

            if (images.Count > MaxImageCount)
            {
                ModelState.AddModelError(
                    nameof(model.Images),
                    "Maximum 5 images are allowed.");
            }

            foreach (var image in images)
            {
                if (image.Length > MaxImageBytes)
                {
                    ModelState.AddModelError(
                        nameof(model.Images),
                        "Each image must be 5 MB or smaller.");
                    break;
                }

                if (!await IsValidImageAsync(image))
                {
                    ModelState.AddModelError(
                        nameof(model.Images),
                        "Only valid JPG, PNG and WebP images are allowed.");
                    break;
                }
            }

            if (!ModelState.IsValid)
            {
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

            var uploadedFiles = new List<string>();

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                _context.Products.Add(product);
                _context.Auctions.Add(auction);

                // Save first to obtain ProductId.
                await _context.SaveChangesAsync();

                string webRoot = _environment.WebRootPath
                    ?? Path.Combine(
                        _environment.ContentRootPath,
                        "wwwroot");

                string uploadRoot = Path.Combine(
                    webRoot,
                    "uploads",
                    "products",
                    product.ProductId.ToString());

                Directory.CreateDirectory(uploadRoot);

                for (int i = 0; i < images.Count; i++)
                {
                    var image = images[i];

                    string extension = Path.GetExtension(
                        image.FileName).ToLowerInvariant();

                    string fileName =
                        $"{Guid.NewGuid():N}{extension}";

                    string physicalPath = Path.Combine(
                        uploadRoot,
                        fileName);

                    await using (var stream = new FileStream(
                        physicalPath,
                        FileMode.CreateNew,
                        FileAccess.Write))
                    {
                        // Track file before copying so that a
                        // partially written file can be cleaned up.
                        uploadedFiles.Add(physicalPath);

                        await image.CopyToAsync(stream);
                    }

                    _context.ProductImages.Add(new ProductImage
                    {
                        ProductId = product.ProductId,
                        ImagePath =
                            $"/uploads/products/{product.ProductId}/{fileName}",
                        SortOrder = i
                    });
                }

                // Save ProductImages into SQL Server.
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();

                // SQL rollback cannot automatically remove files.
                foreach (string filePath in uploadedFiles)
                {
                    try
                    {
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }
                    catch (IOException)
                    {
                        // Cleanup failure can be logged separately.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Cleanup failure can be logged separately.
                    }
                }

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
