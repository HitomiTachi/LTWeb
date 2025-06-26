using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NguyenNhan_2179_tuan3.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

namespace NguyenNhan_2179_tuan3.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class StatisticsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StatisticsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Thêm filter thời gian cho Dashboard
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate)
        {
            // Xử lý khoảng thời gian
            DateTime from = fromDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            DateTime to = toDate?.AddDays(1).AddSeconds(-1) ?? DateTime.Now;

            ViewBag.FromDate = from.ToString("yyyy-MM-dd");
            ViewBag.ToDate = to.ToString("yyyy-MM-dd");

            // Đơn hàng
            ViewBag.TotalOrders = await _context.Orders.CountAsync();
            ViewBag.OrdersToday = await _context.Orders.CountAsync(o => o.OrderDate.Date == DateTime.Today);
            ViewBag.OrdersThisMonth = await _context.Orders.CountAsync(o => o.OrderDate.Month == DateTime.Now.Month && o.OrderDate.Year == DateTime.Now.Year);
            ViewBag.OrdersCanceled = await _context.Orders.CountAsync(o => o.Status == "Đã hủy");

            // Doanh thu tổng, theo ngày, theo tháng - CHỈ tính đơn đã xác nhận/đã giao/thành công
            var confirmedStatuses = new[] { "Đã xác nhận", "Đã giao", "Thành công" }; // chỉnh lại nếu trạng thái của bạn khác

            ViewBag.TotalRevenue = await _context.Orders
                .Where(o => confirmedStatuses.Contains(o.Status))
                .SumAsync(o => (decimal?)o.TotalPrice) ?? 0;

            ViewBag.TodayRevenue = await _context.Orders
                .Where(o => o.OrderDate.Date == DateTime.Today && confirmedStatuses.Contains(o.Status))
                .SumAsync(o => (decimal?)o.TotalPrice) ?? 0;

            ViewBag.MonthRevenue = await _context.Orders
                .Where(o => o.OrderDate.Month == DateTime.Now.Month && o.OrderDate.Year == DateTime.Now.Year
                        && confirmedStatuses.Contains(o.Status))
                .SumAsync(o => (decimal?)o.TotalPrice) ?? 0;

            // Doanh thu theo khoảng thời gian chọn (chỉ các đơn xác nhận)
            var filteredRevenue = await _context.Orders
                .Where(o => o.OrderDate >= from && o.OrderDate <= to && confirmedStatuses.Contains(o.Status))
                .SumAsync(o => (decimal?)o.TotalPrice) ?? 0;
            ViewBag.FilteredRevenue = filteredRevenue;

            // Khách hàng
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            var propCreatedAt = _context.Users.FirstOrDefault()?.GetType().GetProperty("CreatedAt");
            ViewBag.NewUsersThisMonth = propCreatedAt != null
                ? await _context.Users.CountAsync(u =>
                    EF.Property<DateTime>(u, "CreatedAt").Month == DateTime.Now.Month
                    && EF.Property<DateTime>(u, "CreatedAt").Year == DateTime.Now.Year)
                : 0;

            // Sản phẩm
            ViewBag.TotalProducts = await _context.Products.CountAsync();
            ViewBag.LowStockProducts = await _context.Products.CountAsync(p => p.StockQuantity <= 5);

            // Sản phẩm bán chạy nhất (top 5, theo filter nếu muốn)
            ViewBag.BestSellers = await _context.OrderDetails
                .Where(od => od.Order.OrderDate >= from && od.Order.OrderDate <= to && confirmedStatuses.Contains(od.Order.Status))
                .GroupBy(od => od.ProductId)
                .OrderByDescending(g => g.Sum(od => od.Quantity))
                .Select(g => new
                {
                    ProductId = g.Key,
                    ProductName = g.FirstOrDefault().Product.Name,
                    TotalSold = g.Sum(od => od.Quantity)
                })
                .Take(5)
                .ToListAsync();

            // Sản phẩm tồn kho thấp (list)
            ViewBag.LowStockList = await _context.Products
                .Where(p => p.StockQuantity <= 5)
                .OrderBy(p => p.StockQuantity)
                .ToListAsync();

            // Đơn hàng theo phương thức thanh toán (theo filter)
            ViewBag.VnPayOrderCount = await _context.Orders
                .Where(o => o.OrderDate >= from && o.OrderDate <= to)
                .CountAsync(o => o.PaymentMethod == "VnPay");
            ViewBag.CashOrderCount = await _context.Orders
                .Where(o => o.OrderDate >= from && o.OrderDate <= to)
                .CountAsync(o => o.PaymentMethod != "VnPay");

            // Đơn đã hủy (thay vì hoàn tiền)
            ViewBag.CanceledOrders = await _context.Orders.CountAsync(o => o.Status == "Đã hủy");

            // Dữ liệu biểu đồ: Doanh thu theo ngày trong khoảng filter (chỉ đơn xác nhận)
            int daysInRange = (to.Date - from.Date).Days + 1;
            var revenueByDay = Enumerable.Range(0, daysInRange)
                .Select(offset => {
                    var date = from.Date.AddDays(offset);
                    return new
                    {
                        Day = date.ToString("dd/MM"),
                        Revenue = _context.Orders
                            .Where(o => o.OrderDate.Date == date && confirmedStatuses.Contains(o.Status))
                            .Sum(o => (decimal?)o.TotalPrice) ?? 0
                    };
                }).ToList();
            ViewBag.RevenueByDayJson = JsonSerializer.Serialize(revenueByDay);

            // Dữ liệu Pie chart: tỷ lệ đơn theo phương thức thanh toán (chỉ đơn xác nhận)
            var paymentStats = await _context.Orders
                .Where(o => o.OrderDate >= from && o.OrderDate <= to && confirmedStatuses.Contains(o.Status))
                .GroupBy(o => o.PaymentMethod)
                .Select(g => new { Method = g.Key, Count = g.Count() })
                .ToListAsync();
            ViewBag.PaymentLabelsJson = JsonSerializer.Serialize(paymentStats.Select(p => p.Method));
            ViewBag.PaymentCountsJson = JsonSerializer.Serialize(paymentStats.Select(p => p.Count));

            return View();
        }

        // Thống kê sản phẩm bán chạy (toàn bộ hoặc theo tháng nếu muốn)
        public async Task<IActionResult> BestSellers()
        {
            var confirmedStatuses = new[] { "Đã xác nhận", "Đã giao", "Thành công" }; // chỉnh lại nếu trạng thái của bạn khác
            var bestSellers = await _context.OrderDetails
                .Where(od => confirmedStatuses.Contains(od.Order.Status))
                .GroupBy(od => od.ProductId)
                .OrderByDescending(g => g.Sum(od => od.Quantity))
                .Select(g => new
                {
                    ProductId = g.Key,
                    ProductName = g.FirstOrDefault().Product.Name,
                    TotalSold = g.Sum(od => od.Quantity)
                })
                .Take(10)
                .ToListAsync();

            return View(bestSellers);
        }
    }
}