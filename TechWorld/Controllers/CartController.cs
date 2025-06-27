using TechWorld.Data;
using TechWorld.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using TechWorld.Services;

namespace ElectronicsShop.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IShoppingCartService _shoppingCartService;

        public CartController(ApplicationDbContext context, IShoppingCartService shoppingCartService)
        {
            _context = context;
            _shoppingCartService = shoppingCartService;
        }

        // GET: Cart
        public async Task<IActionResult> Index()
        {
            var cart = await _shoppingCartService.GetCartAsync(User, HttpContext.Session);
            return View(cart);
        }

        // POST: Cart/AddToCart
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            try
            {
                // Ủy quyền việc thêm sản phẩm cho Service
                await _shoppingCartService.AddItemAsync(User, HttpContext.Session, productId, quantity);

                // Lấy thông tin sản phẩm để hiển thị TempData (không cần logic giá/giỏ hàng ở đây)
                var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == productId);
                TempData["SuccessMessage"] = $"Đã thêm {quantity} sản phẩm '{product?.ProductName}' vào giỏ hàng.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message; // Service sẽ ném Exception với thông báo lỗi
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Cart/RemoveFromCart
        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int productId)
        {
            // Ủy quyền việc xóa sản phẩm cho Service
            await _shoppingCartService.RemoveItemAsync(User, HttpContext.Session, productId);
            TempData["SuccessMessage"] = "Đã xóa sản phẩm khỏi giỏ hàng.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Cart/UpdateQuantity
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
        {
            try
            {
                // Ủy quyền việc cập nhật số lượng cho Service
                await _shoppingCartService.UpdateQuantityAsync(User, HttpContext.Session, productId, quantity);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message; // Service sẽ ném Exception với thông báo lỗi
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Cart/ClearCart
        [HttpPost]
        public async Task<IActionResult> ClearCart()
        {
            // Ủy quyền việc xóa toàn bộ giỏ hàng cho Service
            await _shoppingCartService.ClearCartAsync(User, HttpContext.Session);
            TempData["SuccessMessage"] = "Giỏ hàng đã được làm trống.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Cart/Checkout
        public async Task<IActionResult> Checkout()
        {
            var cart = await _shoppingCartService.GetCartAsync(User, HttpContext.Session);

            if (cart.Items.Count == 0)
            {
                TempData["ErrorMessage"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction(nameof(Index));
            }

            // Get payment methods for checkout
            var paymentMethods = await _context.PaymentMethods
                .Where(p => p.IsActive)
                .ToListAsync();

            ViewBag.PaymentMethods = paymentMethods;

            if (User.Identity.IsAuthenticated)
            {
                // Lấy ID người dùng hiện tại
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdString, out int userId))
                {
                    // Lấy thông tin người dùng hiện tại
                    var currentUser = await _context.Users
                        .FirstOrDefaultAsync(u => u.UserID == userId);
                    ViewBag.CurrentUser = currentUser;

                    // Lấy địa chỉ mặc định của người dùng với dữ liệu địa lý liên quan
                    var defaultAddress = await _context.UserAddresses
                        .Where(ua => ua.UserID == userId && ua.IsDefault)
                        .Include(ua => ua.Province)
                        .Include(ua => ua.District)
                        .Include(ua => ua.Ward)
                        .FirstOrDefaultAsync();

                    // Nếu không có địa chỉ mặc định, lấy địa chỉ đầu tiên
                    if (defaultAddress == null)
                    {
                        defaultAddress = await _context.UserAddresses
                            .Where(ua => ua.UserID == userId)
                            .Include(ua => ua.Province)
                            .Include(ua => ua.District)
                            .Include(ua => ua.Ward)
                            .OrderByDescending(ua => ua.UserAddressID)
                            .FirstOrDefaultAsync();
                    }

                    ViewBag.DefaultUserAddress = defaultAddress;

                    // Lấy tất cả địa chỉ của người dùng
                    var userAddresses = await _context.UserAddresses
                        .Where(ua => ua.UserID == userId)
                        .OrderByDescending(ua => ua.IsDefault)
                        .ThenByDescending(ua => ua.UserAddressID)
                        .ToListAsync();
                    ViewBag.UserAddresses = userAddresses;

                    // Lấy danh sách tỉnh/thành phố cho việc chọn địa chỉ
                    ViewBag.Provinces = await _context.Provinces
                        .OrderBy(p => p.ProvinceName)
                        .ToListAsync();
                }
            }

            return View(cart);
        }
    }
}
