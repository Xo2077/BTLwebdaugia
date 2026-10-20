
using Microsoft.AspNetCore.Mvc;

namespace BTLwebdaugia.Controllers
{
    public class SellingController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

    }
}
