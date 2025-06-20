using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenNhan_2179_tuan3.Extensions;
using NguyenNhan_2179_tuan3.Models;
using System.Security.Claims;

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

        // Helper để lấy key giỏ hàng phù hợp user
        private string GetCartKey()
        {
            if (User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return $"Cart_{userId}";
            }
            return "Cart";
        }

        // GET: /Admin/ShoppingCart
        public IActionResult Index()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey()) ?? new ShoppingCart();
            return View(cart);
        }

        // POST: /Admin/ShoppingCart/UpdateQuantity
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey()) ?? new ShoppingCart();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            // Lấy thông tin sản phẩm để kiểm tra tồn kho
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
                return NotFound();

            if (quantity > product.StockQuantity)
            {
                return BadRequest($"Chỉ còn {product.StockQuantity} sản phẩm trong kho.");
            }

            if (item != null)
            {
                item.Quantity = quantity > 0 ? quantity : 1;
                HttpContext.Session.SetObjectAsJson(GetCartKey(), cart);
                return Ok();
            }
            return BadRequest();
        }

        // GET: /Admin/ShoppingCart/RemoveItem?productId=xxx
        public IActionResult RemoveItem(int productId)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey()) ?? new ShoppingCart();
            cart.Items.RemoveAll(i => i.ProductId == productId);
            HttpContext.Session.SetObjectAsJson(GetCartKey(), cart);
            return RedirectToAction("Index");
        }

        // GET: /Admin/ShoppingCart/GetCartCount
        [HttpGet]
        public IActionResult GetCartCount()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey()) ?? new ShoppingCart();
            return Json(new { count = cart.Items.Sum(i => i.Quantity) });
        }

        // GET: /Admin/ShoppingCart/Checkout
        [HttpGet]
        public IActionResult Checkout()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey()) ?? new ShoppingCart();
            if (cart == null || !cart.Items.Any())
            {
                TempData["Error"] = "Giỏ hàng trống!";
                return RedirectToAction("Index");
            }
            return View(new Order());
        }

        // POST: /Admin/ShoppingCart/Checkout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(Order order)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey());
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

            // Kiểm tra tồn kho trước khi đặt
            foreach (var item in cart.Items)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null)
                {
                    TempData["Error"] = $"Sản phẩm không tồn tại!";
                    return RedirectToAction("Index");
                }
                if (item.Quantity > product.StockQuantity)
                {
                    TempData["Error"] = $"Sản phẩm {item.Name} chỉ còn {product.StockQuantity} cái!";
                    return RedirectToAction("Index");
                }
            }

            // Trừ tồn kho
            foreach (var item in cart.Items)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                product.StockQuantity -= item.Quantity;
                _context.Products.Update(product);
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
            order.Status = "Chờ xác nhận";

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            HttpContext.Session.Remove(GetCartKey());

            return View("OrderCompleted", order.Id);
        }

        // POST: /Admin/ShoppingCart/CancelOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null || order.Status == "Đã hủy")
                return NotFound();

            // Hoàn lại tồn kho cho từng sản phẩm trong đơn
            foreach (var detail in order.OrderDetails)
            {
                var product = await _context.Products.FindAsync(detail.ProductId);
                if (product != null)
                {
                    product.StockQuantity += detail.Quantity;
                    _context.Products.Update(product);
                }
            }

            order.Status = "Đã hủy";
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã huỷ đơn và hoàn lại tồn kho!";
            return RedirectToAction("OrderList"); // hoặc trang quản lý đơn hàng
        }
    }
}