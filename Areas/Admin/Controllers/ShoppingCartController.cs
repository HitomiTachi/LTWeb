using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenNhan_2179_tuan3.Extensions;
using NguyenNhan_2179_tuan3.Models;

namespace NguyenNhan_2179_tuan3.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ShoppingCartController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ShoppingCartController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: /Admin/ShoppingCart
        public IActionResult Index()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            return View(cart);
        }

        // POST: /Admin/ShoppingCart/UpdateQuantity
        [HttpPost]
        public IActionResult UpdateQuantity(int productId, int quantity)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                item.Quantity = quantity > 0 ? quantity : 1;
                HttpContext.Session.SetObjectAsJson("Cart", cart);
                return Ok();
            }
            return BadRequest();
        }

        // GET: /Admin/ShoppingCart/RemoveItem?productId=xxx
        public IActionResult RemoveItem(int productId)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            cart.Items.RemoveAll(i => i.ProductId == productId);
            HttpContext.Session.SetObjectAsJson("Cart", cart);
            return RedirectToAction("Index");
        }

        // GET: /Admin/ShoppingCart/GetCartCount
        [HttpGet]
        public IActionResult GetCartCount()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            return Json(new { count = cart.Items.Sum(i => i.Quantity) });
        }

        // GET: /Admin/ShoppingCart/Checkout
        [HttpGet]
        public IActionResult Checkout()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            if (cart == null || !cart.Items.Any())
            {
                TempData["Error"] = "Giỏ hàng trống!";
                return RedirectToAction("Index");
            }
            // KHÔNG truyền cart vào view, chỉ trả về form với model Order
            return View(new Order());
        }

        // POST: /Admin/ShoppingCart/Checkout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(Order order)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart == null || !cart.Items.Any())
            {
                TempData["Error"] = "Giỏ hàng trống!";
                return RedirectToAction("Index");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "Bạn chưa đăng nhập!";
                return RedirectToAction("Index");
            }

            order.UserId = user.Id;
            order.OrderDate = DateTime.UtcNow;
            order.TotalPrice = cart.Items.Sum(i => i.Price * i.Quantity);
            order.OrderDetails = cart.Items.Select(i => new OrderDetail
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                Price = i.Price
            }).ToList();

            // SỬA: Thêm giá trị mặc định cho Status
            order.Status = "Chờ xác nhận";

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            HttpContext.Session.Remove("Cart");

            // Truyền order.Id sang view hoàn tất
            return View("OrderCompleted", order.Id);
        }
    }
}