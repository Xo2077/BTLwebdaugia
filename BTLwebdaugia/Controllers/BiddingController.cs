
using Microsoft.AspNetCore.Mvc;

namespace BTLwebdaugia.Controllers
{
    public class BiddingController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}
