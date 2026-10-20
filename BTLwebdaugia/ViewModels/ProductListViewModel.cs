
namespace BTLwebdaugia.ViewModels;

public class ProductListViewModel
{
    public List<CategoryViewModel> Categories { get; set; } = new();

    public List<ProductListItemViewModel> Products { get; set; } = new();

    public ProductFilterViewModel Filter { get; set; } = new();

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }
}
