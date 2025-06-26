using NguyenNhan_2179_tuan3.Services;
using Microsoft.AspNetCore.Mvc;
using NguyenNhan_2179_tuan3.Models;
using Microsoft.AspNetCore.Http;

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

            // Lấy lại order từ Session (nếu cần)
            Order? order = null;
            var orderJson = HttpContext.Session.GetString("OrderPending");
            if (orderJson != null)
            {
                order = Newtonsoft.Json.JsonConvert.DeserializeObject<Order>(orderJson);
            }

            // Thanh toán thành công
            if (response.Success && response.VnPayResponseCode == "00")
            {
                if (order != null)
                {
                    order.Status = "Đã thanh toán";
                    _context.Orders.Add(order);
                    _context.SaveChanges();
                    HttpContext.Session.Remove("OrderPending");
                }
                ViewBag.Message = "Thanh toán thành công! Đơn hàng của bạn đã được xác nhận.";
                return View("Success", response);
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
    }
}