using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenNhan_2179_tuan3.Extensions;
using NguyenNhan_2179_tuan3.Models;
using NguyenNhan_2179_tuan3.Repositories;
using NguyenNhan_2179_tuan3.Services;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NguyenNhan_2179_tuan3.Controllers
{
    public class ShoppingCartController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IVnPayService _vnPayService;

        public ShoppingCartController(ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IProductRepository productRepository,
            IVnPayService vnPayService)
        {
            _productRepository = productRepository;
            _context = context;
            _userManager = userManager;
            _vnPayService = vnPayService;
        }

        private string GetCartKey()
        {
            if (User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return $"Cart_{userId}";
            }
            return "Cart";
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized();
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

        public IActionResult Index()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey()) ?? new ShoppingCart();
            return View(cart);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey()) ?? new ShoppingCart();
            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
                return NotFound();

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(Order order)
        {
            // Lấy cart từ session SelectedCart nếu có, nếu không thì lấy toàn bộ giỏ hàng
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("SelectedCart")
                       ?? HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey());

            if (cart == null || !cart.Items.Any())
            {
                TempData["Error"] = "Giỏ hàng trống!";
                return RedirectToAction("Index");
            }

            // Gán các trường bắt buộc cho order trước khi kiểm tra ModelState
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

            // Xóa lỗi cho các trường được gán động khỏi ModelState (fix lỗi validate bắt buộc)
            ModelState.Remove(nameof(order.Status));
            ModelState.Remove(nameof(order.UserId));
            ModelState.Remove(nameof(order.OrderDetails));

            // Kiểm tra hợp lệ model
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                TempData["Error"] = "Dữ liệu không hợp lệ: " + string.Join("; ", errors);
                return View(order);
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

            // Xử lý thanh toán
            if (order.PaymentMethod == "VnPay")
            {
                var totalQuantity = cart.Items.Sum(i => i.Quantity);
                var productList = string.Join(", ", cart.Items.Select(i => $"{i.Name} x{i.Quantity}"));
                var paymentInfo = new PaymentInformationModel
                {
                    Amount = (int)order.TotalPrice,
                    OrderDescription = $"ĐH#{order.Id} | SL: {totalQuantity} | {productList}",
                    OrderType = "billpayment",
                    Name = user.UserName
                };
                HttpContext.Session.SetString("OrderPending", Newtonsoft.Json.JsonConvert.SerializeObject(order));
                HttpContext.Session.Remove("SelectedCart");
                return RedirectToAction("CreatePaymentUrlVnpay", "Payment", paymentInfo);
            }
            else // Tiền mặt, chuyển khoản, v.v.
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                HttpContext.Session.Remove(GetCartKey());
                HttpContext.Session.Remove("SelectedCart");
                return View("OrderCompleted", order.Id);
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetVnPayUrl([FromBody] Order order)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey());
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

            HttpContext.Session.SetString("OrderPending", Newtonsoft.Json.JsonConvert.SerializeObject(order));

            var paymentInfo = new PaymentInformationModel
            {
                Amount = (int)order.TotalPrice,
                OrderDescription = $"Thanh toán đơn hàng",
                OrderType = "billpayment",
                Name = user.UserName
            };

            var url = _vnPayService.CreatePaymentUrl(paymentInfo, HttpContext);
            if (string.IsNullOrEmpty(url))
            {
                return Json(new { url = (string)null, error = "Không lấy được link thanh toán." });
            }
            return Json(new { url });
        }

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


        [HttpPost]
        public IActionResult CheckoutSelected(int[] selectedProductIds)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>(GetCartKey()) ?? new ShoppingCart();
            var selectedItems = cart.Items.Where(i => selectedProductIds.Contains(i.ProductId)).ToList();

            if (!selectedItems.Any())
            {
                TempData["Error"] = "Bạn chưa chọn sản phẩm nào để thanh toán!";
                return RedirectToAction("Index");
            }

            // Lưu các sản phẩm đã chọn vào session để dùng cho bước thanh toán
            HttpContext.Session.SetObjectAsJson("SelectedCart", new ShoppingCart { Items = selectedItems });

            // Truyền sang view Checkout (có thể truyền selectedItems nếu muốn hiển thị)
            return View("Checkout", new Order());
        }

    }
}