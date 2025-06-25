using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechWorld.Data;
using TechWorld.Models;

namespace TechWorld.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminCategoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminCategoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AdminCategory
        public async Task<IActionResult> Index(string searchString)
        {
            var categories = from c in _context.Categories
                             select c;

            if (!string.IsNullOrEmpty(searchString))
            {
                categories = categories.Where(c => c.CategoryName.Contains(searchString) ||
                                                  (c.Description != null && c.Description.Contains(searchString)));
            }

            var categoriesWithSubcategories = await categories
                .Include(c => c.SubCategories)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            ViewBag.SearchString = searchString;
            return View(categoriesWithSubcategories);
        }

        // GET: AdminCategory/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .Include(c => c.SubCategories)
                .FirstOrDefaultAsync(m => m.CategoryID == id);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // GET: AdminCategory/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: AdminCategory/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CategoryName,Description,IsActive")] Category category)
        {
            if (ModelState.IsValid)
            {
                category.CreatedAt = DateTime.Now;
                _context.Add(category);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Danh mục đã được tạo thành công.";
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // GET: AdminCategory/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }
            return View(category);
        }

        // POST: AdminCategory/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("CategoryID,CategoryName,Description,IsActive,CreatedAt")] Category category)
        {
            if (id != category.CategoryID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(category);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Danh mục đã được cập nhật thành công.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CategoryExists(category.CategoryID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // GET: AdminCategory/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category = await _context.Categories
                .Include(c => c.SubCategories)
                .FirstOrDefaultAsync(m => m.CategoryID == id);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // POST: AdminCategory/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories
                .Include(c => c.SubCategories)
                .FirstOrDefaultAsync(m => m.CategoryID == id);

            if (category == null)
            {
                return NotFound();
            }

            if (category.SubCategories != null && category.SubCategories.Any())
            {
                bool hasProducts = false;
                foreach (var subCategory in category.SubCategories)
                {
                    var products = await _context.Set<Product>()
                        .Where(p => p.SubCategoryID == subCategory.SubCategoryID)
                        .AnyAsync();

                    if (products)
                    {
                        hasProducts = true;
                        break;
                    }
                }

                if (hasProducts)
                {
                    TempData["ErrorMessage"] = "Không thể xóa danh mục này vì có sản phẩm liên quan trong danh mục con.";
                    return RedirectToAction(nameof(Index));
                }

                _context.SubCategories.RemoveRange(category.SubCategories);
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Danh mục đã được xóa thành công.";
            return RedirectToAction(nameof(Index));
        }

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.CategoryID == id);
        }

        // GET: AdminCategory/CreateSubCategory/5
        public async Task<IActionResult> CreateSubCategory(int? categoryId)
        {
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();

            if (categoryId.HasValue)
            {
                var category = await _context.Categories.FindAsync(categoryId);
                if (category == null)
                {
                    return NotFound();
                }
                ViewBag.CategoryName = category.CategoryName;
                ViewBag.CategoryID = category.CategoryID;
            }

            return View();
        }

        // POST: AdminCategory/CreateSubCategory    
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSubCategory([Bind("SubCategoryName,Description,CategoryID,IsActive")] SubCategory subCategory)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var category = await _context.Categories.FindAsync(subCategory.CategoryID);
                    if (category == null)
                    {
                        ModelState.AddModelError("CategoryID", "Danh mục cha không tồn tại.");
                        ViewBag.Categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
                        return View(subCategory);
                    }

                    subCategory.CreatedAt = DateTime.Now;
                    _context.SubCategories.Add(subCategory);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Danh mục con đã được tạo thành công.";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Có lỗi xảy ra: {ex.Message}");
                if (ex.InnerException != null)
                {
                    ModelState.AddModelError("", $"Chi tiết: {ex.InnerException.Message}");
                }
            }

            ViewBag.Categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
            if (subCategory.CategoryID > 0)
            {
                var category = await _context.Categories.FindAsync(subCategory.CategoryID);
                if (category != null)
                {
                    ViewBag.CategoryName = category.CategoryName;
                    ViewBag.CategoryID = category.CategoryID;
                }
            }
            return View(subCategory);
        }

        // GET: AdminCategory/EditSubCategory/5
        public async Task<IActionResult> EditSubCategory(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var subCategory = await _context.SubCategories
                .Include(s => s.Category)
                .FirstOrDefaultAsync(m => m.SubCategoryID == id);

            if (subCategory == null)
            {
                return NotFound();
            }

            ViewBag.Categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
            return View(subCategory);
        }

        // POST: AdminCategory/EditSubCategory/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSubCategory(int id, [Bind("SubCategoryID,SubCategoryName,Description,CategoryID,IsActive,CreatedAt")] SubCategory subCategory)
        {
            if (id != subCategory.SubCategoryID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var categoryExists = await _context.Categories.AnyAsync(c => c.CategoryID == subCategory.CategoryID);
                    if (!categoryExists)
                    {
                        ModelState.AddModelError("CategoryID", "Danh mục cha không tồn tại.");
                        ViewBag.Categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
                        return View(subCategory);
                    }

                    _context.Update(subCategory);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Danh mục con đã được cập nhật thành công.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SubCategoryExists(subCategory.SubCategoryID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            ViewBag.Categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
            return View(subCategory);
        }

        // GET: AdminCategory/DeleteSubCategory/5
        public async Task<IActionResult> DeleteSubCategory(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var subCategory = await _context.SubCategories
                .Include(s => s.Category)
                .FirstOrDefaultAsync(m => m.SubCategoryID == id);

            if (subCategory == null)
            {
                return NotFound();
            }

            return View(subCategory);
        }

        // POST: AdminCategory/DeleteSubCategory/5
        [HttpPost, ActionName("DeleteSubCategory")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubCategoryConfirmed(int id)
        {
            var subCategory = await _context.SubCategories
                .Include(s => s.Products)
                .FirstOrDefaultAsync(m => m.SubCategoryID == id);

            if (subCategory == null)
            {
                return NotFound();
            }

            if (subCategory.Products != null && subCategory.Products.Any())
            {
                TempData["ErrorMessage"] = "Không thể xóa danh mục con này vì có sản phẩm liên quan.";
                return RedirectToAction(nameof(Index));
            }

            _context.SubCategories.Remove(subCategory);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Danh mục con đã được xóa thành công.";
            return RedirectToAction(nameof(Index));
        }

        private bool SubCategoryExists(int id)
        {
            return _context.SubCategories.Any(e => e.SubCategoryID == id);
        }
    }
}
