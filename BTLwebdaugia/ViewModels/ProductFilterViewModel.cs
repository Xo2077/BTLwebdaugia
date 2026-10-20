
namespace BTLwebdaugia.ViewModels;

public class ProductFilterViewModel
{
    public string? SearchTerm { get; set; }

    public int? CategoryId { get; set; }

    public string Status { get; set; } = "All";

    public string SortBy { get; set; } = "EndingSoon";

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 6;
}
