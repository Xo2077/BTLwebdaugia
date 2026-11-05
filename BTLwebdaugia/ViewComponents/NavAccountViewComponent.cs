using BTLwebdaugia.Services;
using Microsoft.AspNetCore.Mvc;

namespace BTLwebdaugia.ViewComponents;

public class NavAccountViewComponent : ViewComponent
{
    private readonly AccountDisplayService _accountDisplay;

    public NavAccountViewComponent(AccountDisplayService accountDisplay)
    {
        _accountDisplay = accountDisplay;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var account = await _accountDisplay.GetAsync(HttpContext.User);

        return View(account);
    }
}
