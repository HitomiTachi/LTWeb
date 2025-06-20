using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using NguyenNhan_2179_tuan3.Models;
using NguyenNhan_2179_tuan3.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace NguyenNhan_2179_tuan3.Controllers
{
    public class HomeController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public HomeController(
            IProductRepository productRepository,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _productRepository = productRepository;
            _userManager = userManager;
            _context = context;
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
                }
            }

            // Lấy tất cả danh mục
            var categories = await _context.Categories.ToListAsync();

            // Tạo ViewModel cho từng danh mục và 3 sản phẩm đầu tiên
            var categoryProducts = new List<CategoryWithProductsViewModel>();
            foreach (var cat in categories)
            {
                var products = await _context.Products
                    .Where(p => p.CategoryId == cat.Id)
                    .Include(p => p.Images)
                    .OrderByDescending(p => p.Id) // hoặc sắp xếp theo CreatedDate nếu có
                    .Take(3)
                    .ToListAsync();

                categoryProducts.Add(new CategoryWithProductsViewModel
                {
                    Category = cat,
                    Products = products
                });
            }

            return View(categoryProducts);
        }

        public async Task<IActionResult> Details(int id)
        {
            // Lấy sản phẩm kèm Category và Images cho trang chi tiết
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        // Bộ lọc ở IndexList
        public async Task<IActionResult> IndexList(int? categoryId, decimal? minPrice, decimal? maxPrice, string searchQuery)
        {
            // Lấy danh sách danh mục cho bộ lọc
            var categories = await _context.Categories.ToListAsync();
            ViewBag.Categories = categories;
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.SearchQuery = searchQuery;

            // Lọc sản phẩm
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(x => x.CategoryId == categoryId);
            if (minPrice.HasValue)
                query = query.Where(x => x.Price >= minPrice);
            if (maxPrice.HasValue)
                query = query.Where(x => x.Price <= maxPrice);
            if (!string.IsNullOrEmpty(searchQuery))
                query = query.Where(x => x.Name.Contains(searchQuery));

            var products = await query.ToListAsync();

            return View(products);
        }
    }

    // ViewModel để truyền sang view
    public class CategoryWithProductsViewModel
    {
        public Category Category { get; set; }
        public List<Product> Products { get; set; }
    }
}