using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenNhan_2179_tuan3.Models; // Đổi lại nếu namespace khác
using System.Linq;

[Authorize]
public class OrderController : Controller
{
    private readonly ApplicationDbContext _context;

    public OrderController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Lịch sử đơn hàng của user hiện tại
    public IActionResult History()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var orders = _context.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .ToList();

        return View(orders);
    }

    public async Task<IActionResult> Details(int id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var order = await _context.Orders
            .Include(o => o.ApplicationUser)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

        if (order == null)
        {
            return NotFound();
        }

        ViewBag.OrderDetails = await _context.OrderDetails
            .Where(od => od.OrderId == id)
            .Include(od => od.Product) // BỔ SUNG DÒNG NÀY
            .ToListAsync();

        return View(order);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(int orderId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var order = await _context.Orders
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

        if (order == null)
        {
            TempData["Error"] = "Đơn hàng không tồn tại!";
            return RedirectToAction("History");
        }

        if (order.Status != "Chờ xác nhận")
        {
            TempData["Error"] = "Đơn hàng đã được xác nhận hoặc không thể hủy!";
            return RedirectToAction("History");
        }

        if (DateTime.UtcNow > order.OrderDate.AddDays(1))
        {
            TempData["Error"] = "Đơn hàng đã quá thời gian hủy (sau 1 ngày)!";
            return RedirectToAction("History");
        }

        if (order.PaymentMethod == "VnPay")
        {
            // Đánh dấu đơn hàng là "Yêu cầu hủy (VNPay)" và gửi thông báo cho admin
            order.Status = "Yêu cầu hủy (VNPay)";
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Yêu cầu hủy đơn hàng đã được gửi tới quản trị viên. Vui lòng chờ xác nhận!";
            // TODO: Gửi email/thông báo cho admin nếu cần
            return RedirectToAction("History");
        }

        // Hoàn lại tồn kho cho từng sản phẩm trong đơn (chỉ cho đơn tiền mặt)
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

        TempData["Success"] = "Đơn hàng đã được hủy thành công!";
        return RedirectToAction("History");
    }

}