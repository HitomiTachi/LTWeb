using NguyenNhan_2179_tuan3.Services;
using Microsoft.AspNetCore.Mvc;
using NguyenNhan_2179_tuan3.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Newtonsoft.Json;

namespace NguyenNhan_2179_tuan3.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IVnPayService _vnPayService;
        private readonly ApplicationDbContext _context;

        public PaymentController(IVnPayService vnPayService, ApplicationDbContext context)
        {
            _vnPayService = vnPayService;
            _context = context;
        }

        public IActionResult CreatePaymentUrlVnpay(PaymentInformationModel model)
        {
            var url = _vnPayService.CreatePaymentUrl(model, HttpContext);
            return Redirect(url);
        }

        [HttpGet]
        public IActionResult PaymentCallbackVnpay()
        {
            var response = _vnPayService.PaymentExecute(Request.Query);

            Order? order = null;
            var orderJson = HttpContext.Session.GetString("OrderPending");
            if (orderJson != null)
            {
                order = JsonConvert.DeserializeObject<Order>(orderJson);
            }

            if (response.Success && response.VnPayResponseCode == "00")
            {
                if (order != null)
                {
                    order.Status = "Chờ xác nhận";
                    //order.Status = "Đã thanh toán";
                    _context.Orders.Add(order);
                    _context.SaveChanges();
                    HttpContext.Session.Remove("OrderPending");
                }

                // Xóa giỏ hàng theo người dùng
                string cartKey = GetCartKey();
                HttpContext.Session.Remove(cartKey);

                TempData["Message"] = "Thanh toán thành công! Cảm ơn bạn đã đặt hàng.";
                return RedirectToAction("Checkout", "ShoppingCart", new { id = order?.Id });

            }
            else if (response.VnPayResponseCode == "24")
            {
                ViewBag.Message = "Bạn đã hủy giao dịch. Đơn hàng chưa được thanh toán.";
                return View("Fail", response);
            }
            else
            {
                ViewBag.Message = $"Giao dịch không thành công. Mã lỗi: {response.VnPayResponseCode}";
                return View("Fail", response);
            }
        }

        // Helper để lấy key giỏ hàng phù hợp với từng người dùng
        private string GetCartKey()
        {
            if (User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    return $"Cart_{userId}";
                }
            }
            return "Cart";
        }
    }
}
