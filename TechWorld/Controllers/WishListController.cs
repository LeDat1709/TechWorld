// File: Controllers/WishlistController.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechWorld.Data;
using TechWorld.Models;
using System.Security.Claims;
using System.Threading.Tasks;
using static TechWorld.Models.WishList;


namespace TechWorld.Controllers
{
    [Authorize]
    public class WishlistController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WishlistController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Action này sẽ xử lý cả việc Thêm và Xóa từ trang chi tiết sản phẩm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int productId)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            {
                return Challenge();
            }

            var productExists = await _context.Products.AnyAsync(p => p.ProductID == productId);
            if (!productExists)
            {
                TempData["ErrorMessage"] = "Sản phẩm không hợp lệ.";
                // Nếu sản phẩm không tồn tại, có thể chuyển về trang chủ hoặc trang trước đó
                string referer = Request.Headers["Referer"].ToString();
                if (!string.IsNullOrEmpty(referer))
                {
                    return Redirect(referer);
                }
                return RedirectToAction("Index", "Home");
            }

            var wishlistItem = await _context.WishLists
                .FirstOrDefaultAsync(w => w.UserID == userId && w.ProductID == productId);

            if (wishlistItem == null)
            {
                // Nếu chưa có -> Thêm vào
                _context.WishLists.Add(new WishList { UserID = userId, ProductID = productId, AddedAt = System.DateTime.Now });
                TempData["SuccessMessage"] = "Đã thêm sản phẩm vào danh sách yêu thích!";
            }
            else
            {
                // Nếu đã có -> Xóa đi
                _context.WishLists.Remove(wishlistItem);
                TempData["SuccessMessage"] = "Đã xóa sản phẩm khỏi danh sách yêu thích.";
            }

            await _context.SaveChangesAsync();

            // Luôn quay lại trang chi tiết sản phẩm
            return RedirectToAction("Details", "Product", new { id = productId });
        }

        // Action này chỉ dùng cho trang Wishlist.cshtml
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromList(int wishlistItemId)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            {
                return Challenge();
            }

            var item = await _context.WishLists.FirstOrDefaultAsync(w => w.WishlistItemID == wishlistItemId && w.UserID == userId);

            if (item != null)
            {
                _context.WishLists.Remove(item);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa sản phẩm khỏi danh sách yêu thích.";
            }
            else
            {
                TempData["ErrorMessage"] = "Không tìm thấy sản phẩm để xóa.";
            }

            return RedirectToAction("Wishlist", "Account");
        }
    }
}