
using System.ComponentModel.DataAnnotations;

namespace BTLwebdaugia.ViewModels;

public class AdminReviewViewModel
{
    [Range(1, long.MaxValue,
        ErrorMessage = "Invalid product.")]
    public long ProductId { get; set; }

    [Required(ErrorMessage = "Please select a decision.")]
    public string Decision { get; set; } = string.Empty;

    [StringLength(1000,
        ErrorMessage = "Rejection reason cannot exceed 1000 characters.")]
    public string? RejectionReason { get; set; }
}
