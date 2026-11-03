using System.ComponentModel.DataAnnotations;

namespace BTLwebdaugia.ViewModels;

public class CategoryInputViewModel
{
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(120,
        ErrorMessage = "Maximum 120 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000,
        ErrorMessage = "Maximum 1000 characters.")]
    public string? Description { get; set; }
}
