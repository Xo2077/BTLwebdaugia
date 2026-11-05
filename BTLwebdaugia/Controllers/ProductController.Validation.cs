using BTLwebdaugia.Models;
using BTLwebdaugia.Services;
using BTLwebdaugia.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BTLwebdaugia.Controllers;

public partial class ProductController
{
    private async Task<(DateTime StartAt, DateTime EndAt, List<IFormFile> Images)>
        ValidateListingAsync(CreateProductViewModel model)
    {
        await ValidateProductInformationAsync(model);
        ValidatePrices(model);
        var (startAt, endAt) = ValidateSchedule(model);
        var images = await ValidateImagesAsync(model);
        return (startAt, endAt, images);
    }

    private async Task ValidateProductInformationAsync(CreateProductViewModel model)
    {
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

        bool categoryExists = await _context.Categories
            .AnyAsync(c => c.CategoryId == model.CategoryId);

        if (!categoryExists)
        {
            ModelState.AddModelError(
                nameof(model.CategoryId),
                "Please select a valid category.");
        }
    }

    private void ValidatePrices(CreateProductViewModel model)
    {
        if (model.StartingPrice < 50 || model.StartingPrice > MoneyRules.MaxStoredAmount ||
            model.StartingPrice !=
                decimal.Truncate(model.StartingPrice))
        {
            ModelState.AddModelError(
                nameof(model.StartingPrice),
                "Starting price must be a whole number of at least $50.");
        }

        if (model.BidStep < 1 || model.BidStep > MoneyRules.MaxStoredAmount ||
            model.BidStep !=
                decimal.Truncate(model.BidStep))
        {
            ModelState.AddModelError(
                nameof(model.BidStep),
                "Bid step must be a positive whole number.");
        }
    }

    private (DateTime StartAt, DateTime EndAt) ValidateSchedule(CreateProductViewModel model)
    {
        DateTime now = DateTime.UtcNow;
        DateTime startAt = now;
        DateTime endAt = now;
        try
        {
            startAt = ValidateStartTime(model, now);
            endAt = startAt;
            endAt = ValidateEndTime(model, startAt);
        }
        catch (ArgumentException)
        {
            ModelState.AddModelError(nameof(model.ScheduledStartAt), "Auction schedule is invalid.");
        }

        TimeSpan duration = endAt - startAt;
        if (duration < AuctionScheduleRules.MinDuration || duration > AuctionScheduleRules.MaxDuration)
        {
            ModelState.AddModelError(nameof(model.SpecificEndAt),
                "Auction duration must be between 60 minutes and 30 days.");
        }
        return (startAt, endAt);
    }

    private DateTime ValidateStartTime(CreateProductViewModel model, DateTime now)
    {
        if (!model.ScheduledStartAt.HasValue)
        {
            ModelState.AddModelError(nameof(model.ScheduledStartAt), "Scheduled start time is required.");
            return now;
        }

        DateTime startAt = AuctionScheduleRules.ToUtc(model.ScheduledStartAt.Value);
        if (startAt <= now)
            ModelState.AddModelError(nameof(model.ScheduledStartAt), "Start time must be in the future.");
        return startAt;
    }

    private DateTime ValidateEndTime(CreateProductViewModel model, DateTime startAt)
    {
        if (model.EndTimeMode == AuctionScheduleRules.DurationMode)
            return ValidateDurationEndTime(model, startAt);

        if (model.EndTimeMode is AuctionScheduleRules.SpecificInputMode or AuctionScheduleRules.FixedEndTimeMode)
            return ValidateFixedEndTime(model, startAt);

        ModelState.AddModelError(nameof(model.EndTimeMode), "Invalid end time mode.");
        return startAt;
    }

    private DateTime ValidateDurationEndTime(CreateProductViewModel model, DateTime startAt)
    {
        if (!model.DurationMinutes.HasValue ||
            model.DurationMinutes.Value < AuctionScheduleRules.MinDurationMinutes ||
            model.DurationMinutes.Value > AuctionScheduleRules.MaxDurationMinutes)
        {
            ModelState.AddModelError(nameof(model.DurationMinutes),
                "Duration must be between 60 and 43200 minutes.");
            return startAt;
        }
        return startAt.AddMinutes(model.DurationMinutes.Value);
    }

    private DateTime ValidateFixedEndTime(CreateProductViewModel model, DateTime startAt)
    {
        if (!model.SpecificEndAt.HasValue)
        {
            ModelState.AddModelError(nameof(model.SpecificEndAt), "End time is required.");
            return startAt;
        }

        DateTime endAt = AuctionScheduleRules.ToUtc(model.SpecificEndAt.Value);
        if (endAt <= startAt)
            ModelState.AddModelError(nameof(model.SpecificEndAt), "End time must be after start time.");
        return endAt;
    }

    private async Task<List<IFormFile>> ValidateImagesAsync(CreateProductViewModel model)
    {
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

        return images;
    }

    private void LocalizeListingErrors()
    {
        foreach (var entry in ModelState.Values)
        {
            var errors = entry.Errors.ToArray();
            entry.Errors.Clear();
            foreach (var error in errors)
            {
                if (!string.IsNullOrEmpty(error.ErrorMessage))
                    entry.Errors.Add(_labels[error.ErrorMessage].Value);
                else
                    entry.Errors.Add(error);
            }
        }
    }

    private async Task SaveProductImagesAsync(
        long productId, List<IFormFile> images, List<string> uploadedFiles)
    {
        string webRoot = _environment.WebRootPath
            ?? Path.Combine(
                _environment.ContentRootPath,
                "wwwroot");

        string uploadRoot = Path.Combine(
            webRoot,
            "uploads",
            "products",
            productId.ToString());

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
                ProductId = productId,
                ImagePath =
                    $"/uploads/products/{productId}/{fileName}",
                SortOrder = i
            });
        }
    }

}
