using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using NguyenNhan_2179_tuan3.Models;
using NguyenNhan_2179_tuan3.Repositories;
using Microsoft.AspNetCore.Identity;

namespace NguyenNhan_2179_tuan3.Controllers
{
    public class HomeController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(IProductRepository productRepository, UserManager<ApplicationUser> userManager)
        {
            _productRepository = productRepository;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            // Nếu đã đăng nhập và là admin thì chuyển sang giao diện admin
            if (User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null && await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    return RedirectToAction("IndexList", "Product", new { area = "Admin" });
                    // hoặc chuyển sang Dashboard nếu có: return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                }
            }

            var products = await _productRepository.GetAllAsync();
            return View(products);
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        public async Task<IActionResult> IndexList()
        {
            var products = await _productRepository.GetAllAsync();
            return View(products);
        }
    }
}