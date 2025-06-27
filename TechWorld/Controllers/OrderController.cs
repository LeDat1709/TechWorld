using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using TechWorld.Data;
using TechWorld.Models;
using TechWorld.Services;
using TechWorld.Settings;
using TechWorld.Areas.Admin.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;

namespace TechWorld.Controllers
{
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OrderController> _logger;
        private readonly IShoppingCartService _shoppingCartService;
        private const string GuestInfoSessionKey = "GuestOrderInfo";
        private readonly BankInfoSettings _bankInfo;

        public OrderController(ApplicationDbContext context, ILogger<OrderController> logger, IShoppingCartService shoppingCartService, IOptions<BankInfoSettings> bankInfoOptions)
        {
            _context = context;
            _logger = logger;
            _shoppingCartService = shoppingCartService;
            _bankInfo = bankInfoOptions.Value;
        }

        // GET: Order/TrackOrder
        public IActionResult TrackOrder()
        {
            return View();
        }

        // POST: Order/TrackOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TrackOrder(OrderTracking model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var order = await _context.Orders
                        .Include(o => o.User)
                        .FirstOrDefaultAsync(o => o.OrderNumber == model.OrderNumber);

                    if (order != null)
                    {
                        if (order.User != null && order.User.Email == model.Email)
                        {
                            return RedirectToAction("OrderDetails", new { id = order.OrderID });
                        }
                        else
                        {
                            var guestInfoJson = HttpContext.Session.GetString($"{GuestInfoSessionKey}_{order.OrderID}");
                            if (!string.IsNullOrEmpty(guestInfoJson))
                            {
                                var guestInfo = JsonConvert.DeserializeObject<GuestOrderInfo>(guestInfoJson);
                                if (guestInfo.Email == model.Email)
                                {
                                    return RedirectToAction("OrderDetails", new { id = order.OrderID });
                                }
                            }
                        }
                    }

                    ModelState.AddModelError("", "Không tìm thấy đơn hàng với thông tin đã nhập. Vui lòng kiểm tra lại.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi tra cứu đơn hàng: {Message}", ex.Message);
                    ModelState.AddModelError("", "Đã xảy ra lỗi khi tra cứu đơn hàng. Vui lòng thử lại sau.");
                }
            }

            return View(model);
        }

        // GET: Order/OrderDetails/5
        public async Task<IActionResult> OrderDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var order = await _context.Orders
                    .Include(o => o.User)
                    .Include(o => o.PaymentMethod)
                    .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                    .ThenInclude(p => p.ProductImages)
                    .Include(o => o.OrderStatuses)
                    .FirstOrDefaultAsync(m => m.OrderID == id);

                if (order == null)
                {
                    return NotFound();
                }

                if (User.Identity.IsAuthenticated)
                {
                    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                    if (order.UserID != userId && !User.IsInRole("Admin"))
                    {
                        return Forbid();
                    }
                }
                else
                {
                    if (!TempData.ContainsKey("TrackOrderId") || (int)TempData["TrackOrderId"] != id)
                    {
                        var guestInfoJson = HttpContext.Session.GetString($"{GuestInfoSessionKey}_{id}");
                        if (string.IsNullOrEmpty(guestInfoJson))
                        {
                            return RedirectToAction("TrackOrder");
                        }
                    }
                }

                if (order.UserID == null)
                {
                    var guestInfoJson = HttpContext.Session.GetString($"{GuestInfoSessionKey}_{order.OrderID}");
                    if (!string.IsNullOrEmpty(guestInfoJson))
                    {
                        var guestInfo = JsonConvert.DeserializeObject<GuestOrderInfo>(guestInfoJson);
                        ViewBag.GuestInfo = guestInfo;
                    }
                }

                return View(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xem chi tiết đơn hàng {OrderId}: {Message}", id, ex.Message);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tải thông tin đơn hàng. Vui lòng thử lại sau.";
                return RedirectToAction("TrackOrder");
            }
        }

        // GET: Order/History
        public async Task<IActionResult> History()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                var orders = await _context.Orders
                    .Include(o => o.User)
                    .Include(o => o.PaymentMethod)
                    .Where(o => o.UserID == userId)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                return View(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải lịch sử đơn hàng: {Message}", ex.Message);
                ViewBag.ErrorMessage = "Đã xảy ra lỗi khi tải lịch sử đơn hàng. Vui lòng thử lại sau.";
                return View(new List<Order>());
            }
        }

        // POST: Order/PlaceOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(string fullName, string email, string phone, string address,
            int provinceId, int districtId, int wardId, int paymentMethodId, string? notes, string? promoCode)
        {
            var cart = await _shoppingCartService.GetCartAsync(User, HttpContext.Session);
            if (cart.Items.Count == 0)
            {
                TempData["ErrorMessage"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction("Index", "Cart");
            }

            var now = DateTime.Now;
            decimal subtotal = 0m;
            decimal rankDiscountPercentage = 0m;
            TechWorld.Models.User currentUser = null;

            if (User.Identity.IsAuthenticated)
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                currentUser = await _context.Users
                    .Include(u => u.Rank)
                    .FirstOrDefaultAsync(u => u.UserID == userId);

                if (currentUser != null && currentUser.Rank != null)
                {
                    rankDiscountPercentage = currentUser.Rank.DiscountPercentage;
                }
            }

            foreach (var item in cart.Items)
            {
                var product = await _context.Products.FindAsync(item.ProductID);

                if (item.Quantity > product.StockQuantity)
                {
                    ModelState.AddModelError("", $"Sản phẩm '{product.ProductName}' chỉ còn lại {product.StockQuantity} sản phẩm trong kho.");

                    // Reload ViewBag data for checkout view
                    await LoadCheckoutViewBagData();
                    return View("~/Views/Cart/Checkout.cshtml", cart);
                }

                subtotal += item.Price * item.Quantity;
            }

            decimal globalVoucherDiscount = 0m;

            if (!string.IsNullOrWhiteSpace(promoCode))
            {
                var codeNormalized = promoCode.Trim().ToUpper();
                var globalPromo = await _context.Promotions
                    .Where(p => p.IsActive
                                && p.ProductID == null
                                && p.StartDate <= now
                                && p.EndDate >= now)
                    .FirstOrDefaultAsync(p => p.PromoCode.ToUpper() == codeNormalized);

                if (globalPromo == null)
                {
                    ModelState.AddModelError("promoCode", "Mã voucher không hợp lệ hoặc đã hết hạn.");
                }
                else if (globalPromo.MinOrderValue.HasValue && subtotal < globalPromo.MinOrderValue.Value)
                {
                    ModelState.AddModelError("promoCode", $"Đơn hàng phải từ {globalPromo.MinOrderValue.Value:N0} VNĐ trở lên để sử dụng voucher.");
                }
                else
                {
                    globalVoucherDiscount = globalPromo.DiscountAmount ?? 0m;
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadCheckoutViewBagData();
                return View("~/Views/Cart/Checkout.cshtml", cart);
            }

            var provinceName = (await _context.Provinces.FindAsync(provinceId))?.ProvinceName ?? "";
            var districtName = (await _context.Districts.FindAsync(districtId))?.DistrictName ?? "";
            var wardName = (await _context.Wards.FindAsync(wardId))?.WardName ?? "";

            var order = new Order
            {
                OrderNumber = GenerateOrderNumber(),
                ShippingAddress = $"{address}, {wardName}, {districtName}, {provinceName}",
                PaymentMethodID = paymentMethodId,
                Notes = notes,
                CreatedAt = now,
                OrderStatus = (paymentMethodId == 2) ? OrderStatusManager.AwaitingPayment : OrderStatusManager.PendingConfirmation,
                UserID = currentUser?.UserID,
                TotalAmount = subtotal - globalVoucherDiscount,
                Discount = globalVoucherDiscount > 0 ? globalVoucherDiscount : (decimal?)null
            };

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                if (order.UserID == null)
                {
                    var guest = new GuestOrderInfo { FullName = fullName, Email = email, Phone = phone };
                    HttpContext.Session.SetString($"{GuestInfoSessionKey}_{order.OrderID}", JsonConvert.SerializeObject(guest));
                }

                foreach (var item in cart.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductID);

                    product.StockQuantity -= item.Quantity;
                    _context.Products.Update(product);

                    _context.OrderDetails.Add(new OrderDetail
                    {
                        OrderID = order.OrderID,
                        ProductID = product.ProductID,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price
                    });
                }

                _context.OrderStatuses.Add(new OrderStatus
                {
                    OrderID = order.OrderID,
                    Status = (paymentMethodId == 2) ? "Chờ thanh toán" : "Chờ xác nhận",
                    Description = (paymentMethodId == 2)
                                    ? "Đơn hàng đã được tạo. Vui lòng thanh toán để được xử lý."
                                    : "Đơn hàng đã được tạo, đang chờ xác nhận từ cửa hàng.",
                    CreatedAt = now
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                if (order.UserID != null && currentUser != null)
                {
                    int earnedPoints = (int)(order.TotalAmount / 1000m);
                    currentUser.Points = (currentUser.Points ?? 0) + earnedPoints;

                    var newRank = await _context.Ranks
                        .Where(r => r.MinimumPoints <= (currentUser.Points ?? 0))
                        .OrderByDescending(r => r.MinimumPoints)
                        .FirstOrDefaultAsync();

                    if (newRank != null && currentUser.RankID != newRank.RankID)
                    {
                        currentUser.RankID = newRank.RankID;
                        _logger.LogInformation("Xếp hạng của người dùng {UserId} được cập nhật từ {OldRankId} thành {NewRankId} với {Points} điểm.", currentUser.UserID, currentUser.RankID, newRank.RankID, currentUser.Points);
                    }
                    _context.Update(currentUser);
                    await _context.SaveChangesAsync();
                }
            }
            catch (DbUpdateException dbEx)
            {
                await tx.RollbackAsync();
                _logger.LogError(dbEx, "Lỗi Database khi đặt hàng.");
                TempData["ErrorMessage"] = "Lỗi khi lưu đơn hàng. Vui lòng thử lại!";
                return RedirectToAction("Checkout", "Cart");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi đặt hàng!");
                ModelState.AddModelError("", ex.Message);
                await LoadCheckoutViewBagData();
                return RedirectToAction("Checkout", "Cart");
            }

            await _shoppingCartService.ClearCartAsync(User, HttpContext.Session);
            TempData["TrackOrderId"] = order.OrderID;
            TempData["SuccessMessage"] = $"Đơn hàng {order.OrderNumber} của bạn đã được đặt thành công!";
            return RedirectToAction("OrderConfirmation", new { id = order.OrderID });
        }

        // Helper method to load ViewBag data for checkout
        private async Task LoadCheckoutViewBagData()
        {
            ViewBag.PaymentMethods = await _context.PaymentMethods
                .Where(pm => pm.IsActive)
                .ToListAsync();

            ViewBag.BankInfo = _bankInfo;
            if (User.Identity.IsAuthenticated)
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                ViewBag.CurrentUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserID == userId);

                ViewBag.DefaultUserAddress = await _context.UserAddresses
                    .Where(ua => ua.UserID == userId && ua.IsDefault)
                    .Include(ua => ua.Province)
                    .Include(ua => ua.District)
                    .Include(ua => ua.Ward)
                    .FirstOrDefaultAsync();

                // If no default address, get the first address
                if (ViewBag.DefaultUserAddress == null)
                {
                    ViewBag.DefaultUserAddress = await _context.UserAddresses
                        .Where(ua => ua.UserID == userId)
                        .Include(ua => ua.Province)
                        .Include(ua => ua.District)
                        .Include(ua => ua.Ward)
                        .OrderByDescending(ua => ua.UserAddressID)
                        .FirstOrDefaultAsync();
                }

                ViewBag.Provinces = await _context.Provinces
                    .OrderBy(p => p.ProvinceName)
                    .ToListAsync();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> ValidateVoucher([FromForm] string promoCode, [FromForm] decimal subtotal)
        {
            var now = DateTime.Now;

            var promo = await _context.Promotions
                .Where(p => p.IsActive
                            && p.ProductID == null
                            && p.StartDate <= now
                            && p.EndDate >= now)
                .FirstOrDefaultAsync(p => p.PromoCode.ToUpper() == promoCode.Trim().ToUpper());

            if (promo == null)
            {
                return Json(new { success = false, error = "Mã voucher không hợp lệ hoặc đã hết hạn." });
            }
            if (promo.MinOrderValue.HasValue && subtotal < promo.MinOrderValue.Value)
            {
                return Json(new { success = false, error = $"Đơn hàng phải từ {promo.MinOrderValue.Value:N0} VNĐ trở lên để sử dụng voucher." });
            }

            if (!promo.DiscountAmount.HasValue || promo.DiscountAmount.Value <= 0)
            {
                return Json(new { success = false, error = "Mã voucher này không hợp lệ." });
            }

            return Json(new { success = true, code = promo.PromoCode, discount = promo.DiscountAmount.Value });
        }

        // GET: Order/OrderConfirmation/5
        [HttpGet]
        public async Task<IActionResult> OrderConfirmation(int? id)
        {
            var order = await _context.Orders
                            .Include(o => o.User)
                            .Include(o => o.PaymentMethod)
                            .Include(o => o.OrderDetails)
                                .ThenInclude(d => d.Product)
                                    .ThenInclude(p => p.ProductImages)
                            .Include(o => o.OrderStatuses)
                            .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order == null) return NotFound();

            if (!User.Identity.IsAuthenticated)
            {
                if (!TempData.ContainsKey("TrackOrderId") || (int)TempData["TrackOrderId"] != id)
                {
                    var guestInfoJson = HttpContext.Session.GetString($"{GuestInfoSessionKey}_{id}");
                    if (string.IsNullOrEmpty(guestInfoJson))
                    {
                        TempData["ErrorMessage"] = "Bạn không có quyền truy cập trang này.";
                        return RedirectToAction("TrackOrder");
                    }
                }
            }

            if (order.UserID == null)
            {
                var guestInfoJson = HttpContext.Session.GetString($"{GuestInfoSessionKey}_{order.OrderID}");
                if (!string.IsNullOrEmpty(guestInfoJson))
                {
                    var guestInfo = JsonConvert.DeserializeObject<GuestOrderInfo>(guestInfoJson);
                    ViewBag.GuestInfo = guestInfo;
                }
            }

            if (order.PaymentMethodID == 2 && order.TotalAmount.HasValue)
            {
                ViewBag.BankInfo = _bankInfo;

                // Lấy thông tin từ cấu hình và đơn hàng
                string bankId = _bankInfo.BankId;
                string accountNumber = _bankInfo.AccountNumber;
                string accountName = HttpUtility.UrlEncode(_bankInfo.AccountName); // Mã hóa để URL hợp lệ
                long amount = (long)order.TotalAmount.Value;
                string description = HttpUtility.UrlEncode(order.OrderNumber); // Mã hóa để URL hợp lệ

                // Tạo URL hình ảnh QR bằng API của vietqr.io
                // Template 'compact2' cho giao diện đẹp và chuyên nghiệp
                string qrImageUrl = $"https://img.vietqr.io/image/{bankId}-{accountNumber}-compact2.png?amount={amount}&addInfo={description}&accountName={accountName}";

                // Gán trực tiếp URL vào ViewBag
                ViewBag.QrCodeImage = qrImageUrl;
            }

            return View(order);
        }

        // GET: Order/CancelOrder/5
        public async Task<IActionResult> CancelOrder(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var order = await _context.Orders
                    .Include(o => o.User)
                    .FirstOrDefaultAsync(m => m.OrderID == id);

                if (order == null)
                {
                    return NotFound();
                }

                if (User.Identity.IsAuthenticated)
                {
                    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                    if (order.UserID != userId && !User.IsInRole("Admin"))
                    {
                        return Forbid();
                    }
                }
                else
                {
                    if (!TempData.ContainsKey("TrackOrderId") || (int)TempData["TrackOrderId"] != id)
                    {
                        return RedirectToAction("TrackOrder");
                    }
                }

                var cancellableStatuses = new List<string> {OrderStatusManager.PendingConfirmation, OrderStatusManager.AwaitingPayment};
                if (!cancellableStatuses.Contains(order.OrderStatus, StringComparer.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = $"Không thể hủy đơn hàng ở trạng thái '{order.OrderStatus}'.";
                    return RedirectToAction("OrderDetails", new { id = order.OrderID });
                }

                return View(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi truy cập trang hủy đơn hàng {OrderId}: {Message}", id, ex.Message);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi truy cập trang hủy đơn hàng. Vui lòng thử lại sau.";
                return RedirectToAction("History");
            }
        }

        // POST: Order/CancelOrder/5
        [HttpPost, ActionName("CancelOrder")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrderConfirmed(int id, string cancelReason)
        {
            try
            {
                var order = await _context.Orders.FindAsync(id);
                if (order == null)
                {
                    return NotFound();
                }

                if (User.Identity.IsAuthenticated)
                {
                    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                    if (order.UserID != userId && !User.IsInRole("Admin"))
                    {
                        return Forbid();
                    }
                }
                else
                {
                    if (!TempData.ContainsKey("TrackOrderId") || (int)TempData["TrackOrderId"] != id)
                    {
                        return RedirectToAction("TrackOrder");
                    }
                }

                var cancellableStatuses = new List<string> { OrderStatusManager.PendingConfirmation, OrderStatusManager.AwaitingPayment };
                if (!cancellableStatuses.Contains(order.OrderStatus, StringComparer.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = $"Không thể hủy đơn hàng ở trạng thái '{order.OrderStatus}'.";
                    return RedirectToAction("OrderDetails", new { id = order.OrderID });
                }

                order.OrderStatus = "Đã hủy";
                order.UpdatedAt = DateTime.Now;

                var orderStatus = new OrderStatus
                {
                    OrderID = order.OrderID,
                    Status = "Đã hủy",
                    Description = string.IsNullOrEmpty(cancelReason) ? "Đơn hàng đã bị hủy." : $"Đơn hàng đã bị hủy. Lý do: {cancelReason}",
                    CreatedAt = DateTime.Now
                };
                _context.OrderStatuses.Add(orderStatus);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đơn hàng đã được hủy thành công.";
                return RedirectToAction("OrderDetails", new { id = order.OrderID });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi hủy đơn hàng {OrderId}: {Message}", id, ex.Message);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi hủy đơn hàng. Vui lòng thử lại sau.";
                return RedirectToAction("OrderDetails", new { id = id });
            }
        }

        private string GenerateOrderNumber()
        {
            var random = new Random();
            var now = DateTime.Now;
            return $"ORD{now:yyMMdd}{random.Next(100000, 999999)}";
        }
    }

    public class GuestOrderInfo
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
    }
}
