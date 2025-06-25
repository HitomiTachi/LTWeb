using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NguyenNhan_2179_tuan3.Extensions;
using NguyenNhan_2179_tuan3.Models;
using NguyenNhan_2179_tuan3.Repositories;
using System.Security.Claims;

namespace NguyenNhan_2179_tuan3.Controllers
{
    public class ShoppingCartController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ShoppingCartController(ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IProductRepository productRepository)
        {
            _productRepository = productRepository;
            _context = context;
            _userManager = userManager;
        }

        // Helper để lấy session key
        private string GetCartKey()
        {
            if (User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return $"Cart_{userId}";
            }
            return "Cart";
        }

        // ✅ Thêm vào giỏ hàng
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized(); // 401 - JS sẽ redirect
            }

            if (quantity <= 0) quantity = 1;

            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
                return NotFound();

            // Lấy giỏ hiện tại và kiểm tra tổng số lượng đã có + thêm mới
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey()) ?? new ShoppingCart();
            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            int totalQuantity = (existingItem?.Quantity ?? 0) + quantity;

            // Kiểm tra tồn kho
            if (product.GetType().GetProperty("StockQuantity") != null && totalQuantity > (int)product.GetType().GetProperty("StockQuantity").GetValue(product))
            {
                int stock = (int)product.GetType().GetProperty("StockQuantity").GetValue(product);
                return Json(new { success = false, message = $"Chỉ còn {stock} sản phẩm trong kho." });
            }

            var cartItem = new CartItem
            {
                ProductId = productId,
                Name = product.Name,
                Price = product.Price,
                Quantity = quantity
            };

            cart.AddItem(cartItem);
            HttpContext.Session.SetObjectAsJson(GetCartKey(), cart);

            return Json(new { success = true, message = "Đã thêm vào giỏ hàng!" });
        }

        // ✅ Trang hiển thị giỏ hàng
        public IActionResult Index()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey()) ?? new ShoppingCart();
            return View(cart);
        }

        // ✅ Cập nhật số lượng
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey()) ?? new ShoppingCart();
            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
                return NotFound();

            // Kiểm tra tồn kho
            if (product.GetType().GetProperty("StockQuantity") != null && quantity > (int)product.GetType().GetProperty("StockQuantity").GetValue(product))
            {
                int stock = (int)product.GetType().GetProperty("StockQuantity").GetValue(product);
                TempData["Error"] = $"Chỉ còn {stock} sản phẩm trong kho.";
                return RedirectToAction("Index");
            }

            cart.UpdateQuantity(productId, quantity);
            HttpContext.Session.SetObjectAsJson(GetCartKey(), cart);
            return RedirectToAction("Index");
        }

        // ✅ Xóa sản phẩm
        public IActionResult RemoveItem(int productId)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey());
            if (cart != null)
            {
                cart.RemoveItem(productId);
                HttpContext.Session.SetObjectAsJson(GetCartKey(), cart);
            }
            return RedirectToAction("Index");
        }

        // ✅ Trang thanh toán
        public IActionResult Checkout()
        {
            return View(new Order());
        }

        // ✅ Xử lý thanh toán
        [HttpPost]
        public async Task<IActionResult> Checkout(Order order)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey());

            if (cart == null || !cart.Items.Any())
            {
                return RedirectToAction("Index");
            }

            // Kiểm tra tồn kho trước khi đặt
            foreach (var item in cart.Items)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                if (product == null)
                {
                    TempData["Error"] = $"Sản phẩm không tồn tại!";
                    return RedirectToAction("Index");
                }
                int stock = product.GetType().GetProperty("StockQuantity") != null
                    ? (int)product.GetType().GetProperty("StockQuantity").GetValue(product)
                    : int.MaxValue;
                if (item.Quantity > stock)
                {
                    TempData["Error"] = $"Sản phẩm {item.Name} chỉ còn {stock} cái!";
                    return RedirectToAction("Index");
                }
            }

            // Trừ tồn kho
            foreach (var item in cart.Items)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                if (product.GetType().GetProperty("StockQuantity") != null)
                {
                    int stock = (int)product.GetType().GetProperty("StockQuantity").GetValue(product);
                    product.GetType().GetProperty("StockQuantity").SetValue(product, stock - item.Quantity);
                    await _productRepository.UpdateAsync(product);
                }
            }

            var user = await _userManager.GetUserAsync(User);
            order.UserId = user.Id;
            order.OrderDate = DateTime.UtcNow;
            order.TotalPrice = cart.Items.Sum(i => i.Price * i.Quantity);

            order.Status = "Chờ xác nhận";

            order.OrderDetails = cart.Items.Select(i => new OrderDetail
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                Price = i.Price
            }).ToList();

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            HttpContext.Session.Remove(GetCartKey());

            return View("OrderCompleted", order.Id);
        }

        // ✅ Đếm số lượng sản phẩm trong giỏ
        [HttpGet]
        public IActionResult GetCartCount()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey()) ?? new ShoppingCart();
            int count = cart.Items.Sum(i => i.Quantity);
            return Json(new { count });
        }

        [HttpPost]
        public async Task<IActionResult> BuyNow(int productId, int quantity = 1)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Redirect("/Identity/Account/Login?returnUrl=" + Url.Action("Index", "ShoppingCart"));
            }

            if (quantity <= 0) quantity = 1;

            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
                return NotFound();

            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey()) ?? new ShoppingCart();
            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            int totalQuantity = (existingItem?.Quantity ?? 0) + quantity;

            if (product.GetType().GetProperty("StockQuantity") != null && totalQuantity > (int)product.GetType().GetProperty("StockQuantity").GetValue(product))
            {
                int stock = (int)product.GetType().GetProperty("StockQuantity").GetValue(product);
                TempData["Error"] = $"Chỉ còn {stock} sản phẩm trong kho.";
                return RedirectToAction("Index");
            }

            var cartItem = new CartItem
            {
                ProductId = productId,
                Name = product.Name,
                Price = product.Price,
                Quantity = quantity
            };

            cart.AddItem(cartItem);
            HttpContext.Session.SetObjectAsJson(GetCartKey(), cart);

            return RedirectToAction("Index");
        }
    }
}