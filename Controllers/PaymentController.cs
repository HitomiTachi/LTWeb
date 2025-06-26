using NguyenNhan_2179_tuan3.Services;
using Microsoft.AspNetCore.Mvc;
using NguyenNhan_2179_tuan3.Models;

namespace NguyenNhan_2179_tuan3.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IVnPayService _vnPayService;
        private readonly ApplicationDbContext _context; // Thêm dòng này

        public PaymentController(IVnPayService vnPayService, ApplicationDbContext context) // Thêm ApplicationDbContext vào constructor
        {
            _vnPayService = vnPayService;
            _context = context; // Gán giá trị
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

            if (response.Success)
            {
                // Lấy lại order từ TempData hoặc database
                if (TempData["Order"] != null)
                {
                    var order = Newtonsoft.Json.JsonConvert.DeserializeObject<Order>(TempData["Order"].ToString());
                    order.Status = "Đã thanh toán";
                    _context.Orders.Add(order);
                    _context.SaveChanges();
                }
                return View("Success", response);
            }
            else
            {
                return View("Fail", response);
            }
        }
    }
}
