using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TechWorld.Data;
using TechWorld.Models;

namespace TechWorld.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminPromotionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminPromotionController> _logger;

        public AdminPromotionController(ApplicationDbContext context, ILogger<AdminPromotionController> logger)
        {
            _context = context;
            _logger = logger; // GÁN LOGGER
        }

        // GET: Admin/AdminPromotions
        public async Task<IActionResult> Index()
        {
            var promotions = await _context.Promotions
                .Include(p => p.Product)
                .Include(p => p.Rank)
                .AsNoTracking()
                .ToListAsync();
            return View(promotions);
        }

        // GET: Admin/AdminPromotions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var promotion = await _context.Promotions
                .Include(p => p.Product)
                .Include(p => p.Rank)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.PromotionID == id);

            if (promotion == null)
            {
                return NotFound();
            }

            return View(promotion);
        }

        // GET: Admin/AdminPromotions/Create
        public async Task<IActionResult> Create()
        {
            ViewData["Products"] = new SelectList(await _context.Products.Where(p => p.IsActive).ToListAsync(), "ProductID", "ProductName");
            ViewData["Ranks"] = new SelectList(await _context.Ranks.ToListAsync(), "RankID", "RankName");
            return View();
        }

        // POST: Admin/AdminPromotions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PromotionName,Description,DiscountPercentage,StartDate,EndDate,IsActive,ProductID,PromoCode,RankID,DiscountAmount,MinOrderValue")] Promotion promotion, string promotionType)
        {
            if (promotion.EndDate <= promotion.StartDate)
            {
                ModelState.AddModelError("EndDate", "Ngày kết thúc phải sau ngày bắt đầu.");
            }

            // Validation tùy theo loại khuyến mãi
            if (promotionType == "Product")
            {
                // Loại 1: Giảm giá theo sản phẩm
                if (!promotion.ProductID.HasValue)
                {
                    ModelState.AddModelError("ProductID", "Vui lòng chọn sản phẩm để áp dụng khuyến mãi.");
                }
                if (promotion.DiscountPercentage <= 0)
                {
                    ModelState.AddModelError("DiscountPercentage", "Phần trăm giảm giá phải lớn hơn 0.");
                }
                // Reset các trường của loại voucher để đảm bảo dữ liệu sạch
                promotion.DiscountAmount = null;
                promotion.MinOrderValue = null;
            }
            else if (promotionType == "Voucher")
            {
                // Loại 2: Voucher giảm giá
                if (!promotion.DiscountAmount.HasValue || promotion.DiscountAmount <= 0)
                {
                    ModelState.AddModelError("DiscountAmount", "Vui lòng nhập số tiền giảm giá và phải lớn hơn 0.");
                }
                if (!promotion.MinOrderValue.HasValue || promotion.MinOrderValue < 0)
                {
                    ModelState.AddModelError("MinOrderValue", "Vui lòng nhập giá trị đơn hàng tối thiểu.");
                }
                // Reset các trường của loại sản phẩm
                promotion.ProductID = null;
                promotion.DiscountPercentage = 1;
            }
            else
            {
                // Trường hợp không xác định được loại
                ModelState.AddModelError(string.Empty, "Vui lòng chọn loại khuyến mãi hợp lệ.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(promotion);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Chương trình khuyến mãi đã được tạo thành công.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["Products"] = new SelectList(await _context.Products.Where(p => p.IsActive).ToListAsync(), "ProductID", "ProductName", promotion.ProductID);
            ViewData["Ranks"] = new SelectList(await _context.Ranks.ToListAsync(), "RankID", "RankName", promotion.RankID);
            return View(promotion);
        }

        // GET: Admin/AdminPromotions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var promotion = await _context.Promotions.FindAsync(id);

            if (promotion == null)
            {
                return NotFound();
            }

            // Xác định loại khuyến mãi để View có thể hiển thị đúng radio button
            if (promotion.ProductID != null)
            {
                ViewBag.PromotionType = "Product";
            }
            else
            {
                ViewBag.PromotionType = "Voucher";
            }

            ViewData["Products"] = new SelectList(await _context.Products.Where(p => p.IsActive).ToListAsync(), "ProductID", "ProductName", promotion.ProductID);
            ViewData["Ranks"] = new SelectList(await _context.Ranks.ToListAsync(), "RankID", "RankName", promotion.RankID);
            return View(promotion);
        }

        // POST: Admin/Promotions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("PromotionID,PromotionName,Description,DiscountPercentage,StartDate,EndDate,IsActive,ProductID,PromoCode,RankID,DiscountAmount,MinOrderValue")] Promotion promotion, string promotionType)
        {
            if (id != promotion.PromotionID)
            {
                return NotFound();
            }

            // Validation cơ bản ngày tháng
            if (promotion.EndDate <= promotion.StartDate)
            {
                ModelState.AddModelError("EndDate", "Ngày kết thúc phải sau ngày bắt đầu.");
            }

            // Áp dụng logic validation và reset dữ liệu tương tự như action Create
            if (promotionType == "Product")
            {
                if (!promotion.ProductID.HasValue)
                {
                    ModelState.AddModelError("ProductID", "Vui lòng chọn sản phẩm để áp dụng khuyến mãi.");
                }
                if (promotion.DiscountPercentage <= 0)
                {
                    ModelState.AddModelError("DiscountPercentage", "Phần trăm giảm giá phải lớn hơn 0.");
                }
                promotion.DiscountAmount = null;
                promotion.MinOrderValue = null;
            }
            else if (promotionType == "Voucher")
            {
                if (!promotion.DiscountAmount.HasValue || promotion.DiscountAmount <= 0)
                {
                    ModelState.AddModelError("DiscountAmount", "Vui lòng nhập số tiền giảm giá và phải lớn hơn 0.");
                }
                if (!promotion.MinOrderValue.HasValue || promotion.MinOrderValue < 0)
                {
                    ModelState.AddModelError("MinOrderValue", "Vui lòng nhập giá trị đơn hàng tối thiểu.");
                }
                promotion.ProductID = null;
                promotion.DiscountPercentage = 0;
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Vui lòng chọn loại khuyến mãi hợp lệ.");
            }


            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(promotion);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Chương trình khuyến mãi đã được cập nhật thành công.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PromotionExists(promotion.PromotionID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Lỗi đồng bộ dữ liệu. Vui lòng thử lại.";
                    }
                }
            }

            // Nếu model không hợp lệ, tải lại dữ liệu cần thiết cho view và trả về
            ViewBag.PromotionType = promotionType; // Giữ lại lựa chọn loại KM của người dùng
            ViewData["Products"] = new SelectList(await _context.Products.Where(p => p.IsActive).ToListAsync(), "ProductID", "ProductName", promotion.ProductID);
            ViewData["Ranks"] = new SelectList(await _context.Ranks.ToListAsync(), "RankID", "RankName", promotion.RankID);
            return View(promotion);
        }

        // GET: Admin/AdminPromotions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var promotion = await _context.Promotions
                .Include(p => p.Product)
                .Include(p => p.Rank)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.PromotionID == id);

            if (promotion == null)
            {
                return NotFound();
            }

            return View(promotion);
        }

        // POST: Admin/AdminPromotions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion == null)
            {
                return NotFound();
            }

            _context.Promotions.Remove(promotion);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Chương trình khuyến mãi đã được xóa thành công.";
            return RedirectToAction(nameof(Index));
        }

        private bool PromotionExists(int id)
        {
            return _context.Promotions.Any(e => e.PromotionID == id);
        }
    }
}
