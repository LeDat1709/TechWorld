using TechWorld.Data;
using TechWorld.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace TechWorld.Controllers
{
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Product/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.SubCategory)
                    .ThenInclude(s => s.Category)
                .Include(p => p.Promotions)
                .Include(p => p.ProductImages)
                .Include(p => p.Reviews)
                    .ThenInclude(r => r.User)
                .AsSplitQuery()
                .FirstOrDefaultAsync(m => m.ProductID == id);

            if (product == null)
            {
                return NotFound();
            }

            string viewedProductsCookie = Request.Cookies["ViewedProducts"] ?? "";
            List<int> viewedProductIds = viewedProductsCookie.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToList();

            viewedProductIds.Remove(id.Value);

            viewedProductIds.Insert(0, id.Value);

            if (viewedProductIds.Count > 15)
            {
                viewedProductIds = viewedProductIds.Take(15).ToList();
            }

            var cookieOptions = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(30),
                HttpOnly = true,
                IsEssential = true
            };

            Response.Cookies.Append("ViewedProducts", string.Join(",", viewedProductIds), cookieOptions);


            decimal rankDiscountPercentage = 0m;
            if (User.Identity.IsAuthenticated)
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var user = await _context.Users
                    .Include(u => u.Rank) // Đảm bảo include Rank để lấy DiscountPercentage
                    .FirstOrDefaultAsync(u => u.UserID == userId);

                if (user != null && user.Rank != null)
                {
                    rankDiscountPercentage = user.Rank.DiscountPercentage;
                }
            }
            ViewBag.RankDiscountPercentage = rankDiscountPercentage;

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userIdw))
            {
                var isFavorite = await _context.WishLists.AnyAsync(w => w.UserID == userIdw && w.ProductID == product.ProductID);
                ViewBag.IsFavorite = isFavorite;
            }


            // Get related products from the same subcategory
            var relatedProducts = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Promotions)
                .Where(p => p.SubCategoryID == product.SubCategoryID && p.ProductID != product.ProductID && p.IsActive)
                .Take(4)
                .ToListAsync();

            ViewBag.RelatedProducts = relatedProducts;

            if (User.Identity.IsAuthenticated)
            {
                if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                {
                    ViewBag.IsFavorite = await _context.WishLists.AnyAsync(w => w.UserID == userId && w.ProductID == id);
                }
            }
            else
            {
                ViewBag.IsFavorite = false;
            }

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(int productId, int rating, string comment)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Details", "Product", new { id = productId }) });
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized();
            }

            // Kiểm tra nếu rating hợp lệ
            if (rating < 1 || rating > 5)
            {
                TempData["ErrorMessage"] = "Số sao đánh giá không hợp lệ.";
                return RedirectToAction("Details", "Product", new { id = productId });
            }

            // Kiểm tra đã đánh giá chưa
            var alreadyReviewed = await _context.Reviews.AnyAsync(r => r.UserID == userId && r.ProductID == productId);
            if (alreadyReviewed)
            {
                TempData["ErrorMessage"] = "Bạn đã đánh giá sản phẩm này rồi.";
                return RedirectToAction("Details", "Product", new { id = productId });
            }

            var review = new Review
            {
                ProductID = productId,
                UserID = userId,
                Rating = rating,
                Comment = comment?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cảm ơn bạn đã đánh giá sản phẩm!";
            return RedirectToAction("Details", "Product", new { id = productId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteReview(int reviewId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            var review = await _context.Reviews.FindAsync(reviewId);

            if (review == null || review.UserID != userId)
                return NotFound();

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đánh giá đã được xóa.";
            return RedirectToAction("Reviews", "Account");
        }
    }
}
