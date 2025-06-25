using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using TechWorld.Data;

namespace TechWorld.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Home
        public async Task<IActionResult> Index()
        {
            // Thống kê tổng quan
            ViewBag.TotalRevenue = await _context.Orders
                .Where(o => o.OrderStatus == "Delivered")
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

            ViewBag.TotalOrders = await _context.Orders
                .CountAsync();

            ViewBag.TotalCustomers = await _context.Users
                .CountAsync(u => u.Role == "Customer");

            ViewBag.BestSellingProduct = await _context.OrderDetails
                .Include(od => od.Product)
                .GroupBy(od => od.ProductID)
                .Select(g => new { ProductName = g.First().Product.ProductName, TotalSold = g.Sum(od => od.Quantity) })
                .OrderByDescending(g => g.TotalSold)
                .Select(g => g.ProductName)
                .FirstOrDefaultAsync() ?? "Chưa có dữ liệu";

            // Đơn hàng gần đây (5 đơn hàng mới nhất)
            ViewBag.RecentOrders = await _context.Orders
                .Include(o => o.User)
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Trạng thái đơn hàng (dùng cho biểu đồ)
            var orderStatuses = await _context.Orders
                .GroupBy(o => o.OrderStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.OrderStatusLabels = orderStatuses.Select(s => $"'{s.Status}'").ToList();
            ViewBag.OrderStatusCounts = orderStatuses.Select(s => s.Count).ToList();

            return View();
        }

        // GET: Admin/Home/GetRevenueByMonth
        [HttpGet]
        public async Task<IActionResult> GetRevenueByMonth(int year)
        {
            var startDate = new DateTime(year, 1, 1);
            var endDate = new DateTime(year, 12, 31, 23, 59, 59);

            var revenueData = await _context.Orders
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate && o.OrderStatus == "Delivered")
                .GroupBy(o => new { o.CreatedAt.Month })
                .Select(g => new
                {
                    Month = g.Key.Month,
                    Revenue = g.Sum(o => o.TotalAmount)
                })
                .OrderBy(g => g.Month)
                .ToListAsync();

            var labels = Enumerable.Range(1, 12).Select(m => CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m)).ToList();
            var revenues = Enumerable.Range(1, 12).Select(m => revenueData.FirstOrDefault(r => r.Month == m)?.Revenue ?? 0m).ToList();

            return Json(new { labels, revenues });
        }
    }
}
