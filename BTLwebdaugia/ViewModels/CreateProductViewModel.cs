
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace BTLwebdaugia.ViewModels;

public class CreateProductViewModel
{
    [Required(ErrorMessage = "Please enter a product name.")]
    [StringLength(200, ErrorMessage = "Product name cannot exceed 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Please select a category.")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Please enter a description.")]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "50", "999999999999999999",
        ErrorMessage = "Starting price must be at least $50.")]
    public decimal StartingPrice { get; set; }

    public decimal BidStep { get; set; }

    [Required(ErrorMessage = "Please select a start mode.")]
    public string StartMode { get; set; } = string.Empty;

    public DateTime? ScheduledStartAt { get; set; }

    [Required(ErrorMessage = "Please select an end time mode.")]
    public string EndTimeMode { get; set; } = string.Empty;

    public int? DurationMinutes { get; set; }

    public DateTime? SpecificEndAt { get; set; }

    public List<IFormFile> Images { get; set; } = new();
}
