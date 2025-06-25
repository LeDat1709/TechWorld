using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechWorld.Areas.Admin.Utils;
using TechWorld.Data;
using TechWorld.Models;

namespace TechWorld.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminOrderController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminOrderController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AdminOrders
        public async Task<IActionResult> Index(string searchString, string statusFilter, int pageNumber = 1)
        {
            ViewBag.StatusList = OrderStatusManager.AdminStatusList;
            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentStatus = statusFilter;

            // Bắt đầu câu truy vấn, chưa sắp xếp
            var query = _context.Orders
                .Include(o => o.User)
                .AsNoTracking();

            // Áp dụng các bộ lọc (Where) trước
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(o => o.OrderNumber.Contains(searchString) ||
                                          (o.User != null && o.User.FullName.Contains(searchString)));
            }

            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
            {
                query = query.Where(o => o.OrderStatus == statusFilter);
            }

            // Sắp xếp (OrderBy) ngay trước khi phân trang
            var orderedQuery = query.OrderByDescending(o => o.CreatedAt);

            int pageSize = 10;
            // Truyền câu truy vấn ĐÃ ĐƯỢC SẮP XẾP vào PaginatedList
            var paginatedOrders = await PaginatedList<Order>.CreateAsync(orderedQuery, pageNumber, pageSize);

            return View(paginatedOrders);
        }

        // GET: AdminOrders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.PaymentMethod)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                .Include(o => o.OrderStatuses.OrderByDescending(s => s.CreatedAt))
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.OrderID == id);

            if (order == null) return NotFound();

            ViewBag.StatusList = OrderStatusManager.AdminStatusList;
            return View(order);
        }

        // POST: Admin/Orders/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int orderId, string newStatus, string statusDescription)
        {
            if (string.IsNullOrEmpty(newStatus))
            {
                TempData["ErrorMessage"] = "Vui lòng chọn trạng thái mới.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound();

            if (order.OrderStatus != newStatus)
            {
                order.OrderStatus = newStatus;
                order.UpdatedAt = DateTime.Now;

                _context.OrderStatuses.Add(new OrderStatus
                {
                    OrderID = order.OrderID,
                    Status = newStatus,
                    Description = string.IsNullOrEmpty(statusDescription)
                                ? "Trạng thái được cập nhật bởi quản trị viên."
                                : statusDescription,
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật trạng thái đơn hàng thành công!";
            }
            else
            {
                TempData["InfoMessage"] = "Trạng thái đơn hàng không thay đổi.";
            }

            return RedirectToAction(nameof(Details), new { id = orderId });
        }


        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.OrderID == id);
        }

        // GET: AdminOrders/Delete/5
        public async Task<IActionResult> Cancel(int? id)
        {
            if (id == null) return NotFound();

            var order = await _context.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.OrderID == id);

            // Admin chỉ có thể hủy các đơn hàng chưa được giao
            var cancellableByAdmin = new List<string> {
                 OrderStatusManager.PendingConfirmation,
                 OrderStatusManager.AwaitingPayment,
                 OrderStatusManager.Confirmed,
                 OrderStatusManager.Processing
            };

            if (order == null || !cancellableByAdmin.Contains(order.OrderStatus))
            {
                TempData["ErrorMessage"] = "Không thể hủy đơn hàng ở trạng thái này.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return View(order);
        }

        // POST: AdminOrders/Delete/5
        [HttpPost, ActionName("Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelConfirmed(int orderId, string cancelReason)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails) // Cần include để khôi phục kho
                .FirstOrDefaultAsync(o => o.OrderID == orderId);

            if (order == null) return NotFound();

            if (order.OrderStatus != OrderStatusManager.Cancelled)
            {
                foreach (var item in order.OrderDetails)
                {
                    var product = await _context.Products.FindAsync(item.ProductID);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity;
                    }
                }

                order.OrderStatus = OrderStatusManager.Cancelled;
                order.UpdatedAt = DateTime.Now;

                _context.OrderStatuses.Add(new OrderStatus
                {
                    OrderID = order.OrderID,
                    Status = OrderStatusManager.Cancelled,
                    Description = string.IsNullOrEmpty(cancelReason)
                                ? "Đơn hàng bị hủy bởi quản trị viên."
                                : $"Lý do hủy: {cancelReason}",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đơn hàng #{order.OrderNumber} đã được hủy thành công và kho hàng đã được cập nhật.";
            }
            else
            {
                TempData["InfoMessage"] = "Đơn hàng này đã ở trạng thái hủy từ trước.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
