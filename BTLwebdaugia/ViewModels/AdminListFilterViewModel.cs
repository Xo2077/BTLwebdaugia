namespace BTLwebdaugia.ViewModels;

public class AdminListFilterViewModel
{
    public string? Search { get; set; }
    public string Status { get; set; } = "";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));

    public void Normalize()
    {
        Page = Math.Max(1, Page);
        if (PageSize != 10 && PageSize != 20 && PageSize != 50)
            PageSize = 10;
        Search = Search?.Trim();
        Status ??= "";
    }
}
