using BTLwebdaugia.Models;
using BTLwebdaugia.Services;
using BTLwebdaugia.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BTLwebdaugia.Controllers;

public partial class ProductController
{
    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var sellerId))
            return Challenge();

        var product = await LoadResubmissionAsync(id, sellerId, false);
        if (product == null)
            return NotFound();
        if (!ListingReviewPolicy.CanResubmit(product))
            return ResubmissionConflict(id, "This listing is no longer eligible for resubmission.");

        var auction = product.Auctions.OrderByDescending(a => a.AuctionId).First();
        var model = new ResubmitListingViewModel
        {
            ProductId = product.ProductId,
            AuctionId = auction.AuctionId,
            Revision = ListingReviewPolicy.Revision(product, auction),
            Name = product.Name,
            CategoryId = product.CategoryId,
            Description = product.Description,
            StartingPrice = auction.StartingPrice,
            BidStep = auction.BidStep,
            ScheduledStartAt = auction.StartAt.AddHours(7),
            EndTimeMode = auction.EndTimeMode == AuctionScheduleRules.DurationMode ? AuctionScheduleRules.DurationMode : AuctionScheduleRules.SpecificInputMode,
            DurationMinutes = auction.DurationMinutes,
            SpecificEndAt = auction.OriginalEndAt.AddHours(7)
        };
        await PrepareResubmissionViewAsync(model, product);
        Response.Headers.CacheControl = "no-store";
        return View("~/Views/Selling/Edit.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Edit(long id, ResubmitListingViewModel model)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var sellerId))
            return Challenge();

        var uploadedFiles = new List<string>();
        var oldPaths = new List<string>();
        try
        {
            await using var transaction = await _context.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var product = await LoadResubmissionAsync(id, sellerId, true);
            if (product == null)
                return NotFound();

            var auction = product.Auctions.OrderByDescending(a => a.AuctionId).FirstOrDefault();
            if (!ListingReviewPolicy.CanResubmit(product) || auction == null ||
                model.ProductId != id || model.AuctionId != auction.AuctionId ||
                model.Revision != ListingReviewPolicy.Revision(product, auction))
            {
                return ResubmissionConflict(id, "The listing changed. Reload its details before editing.");
            }

            bool editProduct = ListingReviewPolicy.CanEditProduct(product);
            if (!editProduct)
            {
                // Approved product data is restored from SQL even if the submitted form is tampered with.
                model.Name = product.Name;
                model.Description = product.Description;
                model.CategoryId = product.CategoryId;
                foreach (string field in new[] { nameof(model.Name), nameof(model.Description), nameof(model.CategoryId) })
                    ModelState.Remove(field);

                if (model.Images?.Any(f => f.Length > 0) == true)
                    ModelState.AddModelError(nameof(model.Images), "Approved product images cannot be replaced.");
                model.Images = new();
            }

            var (startAt, endAt, images) = await ValidateListingAsync(model);
            if (!ModelState.IsValid)
            {
                LocalizeListingErrors();
                await transaction.RollbackAsync();
                await PrepareResubmissionViewAsync(model, product);
                return View("~/Views/Selling/Edit.cshtml", model);
            }

            if (editProduct)
            {
                product.Name = model.Name.Trim();
                product.Description = model.Description.Trim();
                product.CategoryId = model.CategoryId;
                product.ApprovalStatus = "Pending";
                product.RejectionReason = null;
                product.ReviewedBy = null;
                product.ReviewedAt = null;
                if (images.Count > 0)
                {
                    oldPaths.AddRange(product.ProductImages.Select(i => i.ImagePath));
                    _context.ProductImages.RemoveRange(product.ProductImages);
                    // Remove old sort-order keys before inserting the replacement set.
                    await _context.SaveChangesAsync();
                    await SaveProductImagesAsync(product.ProductId, images, uploadedFiles);
                }
            }

            auction.StartingPrice = model.StartingPrice;
            auction.BidStep = model.BidStep;
            auction.EndTimeMode = model.EndTimeMode == AuctionScheduleRules.DurationMode ? AuctionScheduleRules.DurationMode : AuctionScheduleRules.FixedEndTimeMode;
            auction.DurationMinutes = model.EndTimeMode == AuctionScheduleRules.DurationMode ? model.DurationMinutes : null;
            auction.StartAt = startAt;
            auction.OriginalEndAt = endAt;
            auction.CurrentEndAt = endAt;
            auction.ExtendedOnce = false;
            auction.ApprovalStatus = "Pending";
            auction.RejectionReason = null;
            auction.ReviewedBy = null;
            auction.ReviewedAt = null;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception exception) when (SqlFailure.IsConcurrencyConflict(exception))
        {
            _logger.LogWarning(exception, "Concurrent listing update for product {ProductId}", id);
            DeleteListingFiles(id, uploadedFiles, physicalPaths: true);
            return ResubmissionConflict(id, "The listing is being reviewed. Please reload and try again.");
        }
        catch
        {
            DeleteListingFiles(id, uploadedFiles, physicalPaths: true);
            throw;
        }

        DeleteListingFiles(id, oldPaths, physicalPaths: false);
        TempData["SuccessMessage"] = "Your listing has been resubmitted for review.";
        return RedirectToAction("Details", "Selling", new { id });
    }

    private IActionResult ResubmissionConflict(long id, string message)
    {
        TempData["ErrorMessage"] = message;
        return RedirectToAction("Details", "Selling", new { id });
    }

    private async Task<Product?> LoadResubmissionAsync(long id, long sellerId, bool lockRows)
    {
        var query = lockRows
            ? _context.Products.FromSqlInterpolated(
                $@"SELECT * FROM dbo.Products WITH (UPDLOCK, HOLDLOCK)
                   WHERE ProductId = {id} AND SellerId = {sellerId}")
            : _context.Products.AsNoTracking();

        var product = await query.Where(p => p.ProductId == id &&
                p.SellerId == sellerId && !p.IsDeleted)
            .Include(p => p.ProductImages).FirstOrDefaultAsync();
        if (product == null)
            return null;

        var auctions = lockRows
            ? _context.Auctions.FromSqlInterpolated(
                $@"SELECT * FROM dbo.Auctions WITH (UPDLOCK, HOLDLOCK)
                   WHERE ProductId = {id}")
            : _context.Auctions.AsNoTracking();
        product.Auctions = await auctions.Where(a => a.ProductId == id)
            .Include(a => a.Bids).Include(a => a.Order).AsSplitQuery().ToListAsync();
        return product;
    }

    private async Task PrepareResubmissionViewAsync(ResubmitListingViewModel model, Product product)
    {
        model.CanEditProduct = ListingReviewPolicy.CanEditProduct(product);
        model.ExistingImages = product.ProductImages.OrderBy(i => i.SortOrder).Select(i => i.ImagePath).ToList();
        model.ProductReason = product.RejectionReason;
        model.AuctionReason = product.Auctions.OrderByDescending(a => a.AuctionId).First().RejectionReason;
        await LoadCategoriesAsync();
    }

    private void DeleteListingFiles(long productId, IEnumerable<string> paths, bool physicalPaths)
    {
        string root = Path.GetFullPath(Path.Combine(
            _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"),
            "uploads", "products", productId.ToString())) + Path.DirectorySeparatorChar;
        foreach (string path in paths)
        {
            string target = physicalPaths ? Path.GetFullPath(path) : Path.GetFullPath(
                Path.Combine(root, Path.GetFileName(path)));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                continue;
            UploadedFileCleanup.Delete(target, _logger);
        }
    }
}
