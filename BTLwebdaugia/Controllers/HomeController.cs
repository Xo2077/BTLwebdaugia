
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTLwebdaugia.Data;
using System;
using System.Threading.Tasks;

namespace BTLwebdaugia.Controllers
{
    public class HomeController : Controller
    {
        private readonly BidHubDbContext _context;

        public HomeController(BidHubDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }


        public IActionResult Category()
        {
            return View();
        }


        public IActionResult Privacy()
        {
            return View();
        }
    }
}
