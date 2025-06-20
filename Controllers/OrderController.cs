using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    // Chi tiết đơn hàng
    public IActionResult Details(int id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var order = _context.Orders.FirstOrDefault(o => o.Id == id && o.UserId == userId);
        if (order == null)
            return NotFound();

        var details = _context.OrderDetails.Where(d => d.OrderId == id).ToList();
        ViewBag.OrderDetails = details;
        return View(order);
    }
}