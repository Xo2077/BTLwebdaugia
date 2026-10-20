using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTLwebdaugia.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }


        [HttpGet]
        public IActionResult Users()
        {
            return View();
        }


        [HttpGet]
        public IActionResult Categories()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Products()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Auctions()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Bids()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Orders()
        {
            return View();
        }
        [HttpGet]
        public IActionResult Wallets()
        {
            return View();
        }





    }
}
