using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenNhan_2179_tuan3.Models;
using System.Threading.Tasks;
using System.Linq;

namespace NguyenNhan_2179_tuan3.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;

        public OrderController(ApplicationDbContext context, IEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
        }

        // GET: Admin/Order
        public async Task<IActionResult> Index()
        {
            var orders = await _context.Orders
                .Include(o => o.ApplicationUser)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
            return View(orders);
        }

        // GET: Admin/Order/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.ApplicationUser)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();
            return View(order);
        }

        // POST: Admin/Order/Confirm/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                TempData["Message"] = "Không tìm thấy đơn hàng!";
                return RedirectToAction(nameof(Index));
            }

            order.Status = "Đã xác nhận";
            await _context.SaveChangesAsync();

            TempData["Message"] = "Đơn hàng đã được xác nhận!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Order/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _context.Orders
                .Include(o => o.ApplicationUser)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();
            return View(order);
        }

        // POST: Admin/Order/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .Include(o => o.ApplicationUser)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order != null)
            {
                _context.OrderDetails.RemoveRange(order.OrderDetails);
                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();

                // Gửi email thông báo hủy đơn cho user (ghi rõ thông tin)
                var user = order.ApplicationUser;
                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    string subject = $"Thông báo: Đơn hàng #{order.Id} đã bị hủy";
                    string body = $@"
                        <p>Xin chào <strong>{user.UserName}</strong>,</p>
                        <p>Chúng tôi rất tiếc phải thông báo rằng đơn hàng của bạn, đặt ngày <strong>{order.OrderDate:dd/MM/yyyy}</strong>, đã bị <span style='color:red;'><b>hủy</b></span>.</p>
                        <p><b>Lý do:</b> Đơn hàng đã được yêu cầu hủy hoặc không đáp ứng đủ điều kiện xử lý.</p>
                        <p>Nếu bạn có bất kỳ thắc mắc nào, vui lòng liên hệ với chúng tôi qua email hoặc số điện thoại hỗ trợ trên website.</p>
                        <hr>
                        <p><i>Xin cảm ơn bạn đã sử dụng dịch vụ của chúng tôi!</i></p>
                    ";

                    await _emailSender.SendEmailAsync(
                        user.Email,
                        subject,
                        body
                    );
                }

                TempData["Message"] = "Đã hủy đơn hàng và gửi email thành công!";
            }
            else
            {
                TempData["Message"] = "Không tìm thấy đơn hàng để hủy!";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> TestEmail([FromServices] IEmailSender emailSender)
        {
            await emailSender.SendEmailAsync("nguyenthanhnhan.20474@gmail.com", "Test gửi email", "Đây là email test!");
            return Content("Đã gửi thử!");
        }
    }
}