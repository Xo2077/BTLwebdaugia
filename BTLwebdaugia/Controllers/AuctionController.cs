
using Microsoft.AspNetCore.Mvc;

namespace BTLwebdaugia.Controllers
{
    public class AuctionController : Controller
    {
        public IActionResult Details()
        {
            return View();
        }


        public IActionResult Ended()
        {
            return View();
        }

    }
}
