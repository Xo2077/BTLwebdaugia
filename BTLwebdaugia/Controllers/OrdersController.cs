
using Microsoft.AspNetCore.Mvc;

namespace BTLwebdaugia.Controllers
{
    public class OrdersController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}
