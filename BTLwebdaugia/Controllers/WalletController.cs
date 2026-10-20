
using Microsoft.AspNetCore.Mvc;

namespace BTLwebdaugia.Controllers
{
    public class WalletController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}
