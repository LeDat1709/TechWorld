using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechWorld.Data;
using TechWorld.Models;

namespace TechWorld.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminProductController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminProductController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/AdminProduct
        public IActionResult Index(string searchString, int? subCategoryId, decimal? minPrice, decimal? maxPrice, bool? isActive)
        {
            var products = _context.Products
                .Include(p => p.SubCategory).ThenInclude(sc => sc.Category)
                .Include(p => p.ProductImages)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
                products = products.Where(p => p.ProductName.Contains(searchString));
            if (subCategoryId.HasValue)
                products = products.Where(p => p.SubCategoryID == subCategoryId);
            if (minPrice.HasValue)
                products = products.Where(p => p.Price >= minPrice);
            if (maxPrice.HasValue)
                products = products.Where(p => p.Price <= maxPrice);
            if (isActive.HasValue)
                products = products.Where(p => p.IsActive == isActive);

            ViewBag.SubCategories = _context.SubCategories.Include(sc => sc.Category).ToList();
            ViewBag.SearchString = searchString;
            ViewBag.SubCategoryId = subCategoryId;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.IsActive = isActive;

            return View(products.ToList());
        }

        // GET: Admin/AdminProduct/Create
        public IActionResult Create()
        {
            ViewBag.SubCategories = _context.SubCategories.Include(sc => sc.Category).ToList();
            return View(new Product());
        }

        // POST: Admin/AdminProduct/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product model, IFormFile[] productImages)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                model.ProductImages = new List<ProductImage>();

                // Define the directory path
                var directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");

                // Create the directory if it doesn't exist
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                for (int i = 0; i < productImages.Length; i++)
                {
                    if (productImages[i] != null)
                    {
                        var fileName = Guid.NewGuid() + Path.GetExtension(productImages[i].FileName);
                        var filePath = Path.Combine(directoryPath, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await productImages[i].CopyToAsync(stream);
                        }
                        model.ProductImages.Add(new ProductImage { ImagePath = $"{fileName}", IsMainImage = i == 0 });
                    }
                }
                _context.Products.Add(model);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm sản phẩm thành công!";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.SubCategories = _context.SubCategories.Include(sc => sc.Category).ToList();
            return View(model);
        }

        // GET: Admin/AdminProduct/Edit/5
        public IActionResult Edit(int id)
        {
            var product = _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.SubCategory)
                .FirstOrDefault(p => p.ProductID == id);
            if (product == null) return NotFound();
            ViewBag.SubCategories = _context.SubCategories.Include(sc => sc.Category).ToList();
            return View(product);
        }

        // POST: Admin/AdminProduct/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Product model, IFormFile[] newImages, int? mainImageId, int[] imagesToDelete)
        {
            if (ModelState.IsValid)
            {
                var product = _context.Products
                    .Include(p => p.ProductImages)
                    .FirstOrDefault(p => p.ProductID == model.ProductID);
                if (product == null) return NotFound();

                product.ProductName = model.ProductName;
                product.Description = model.Description;
                product.Price = model.Price;
                product.StockQuantity = model.StockQuantity;
                product.SubCategoryID = model.SubCategoryID;
                product.IsActive = model.IsActive;
                product.UpdatedAt = DateTime.Now;

                // Define the directory path
                var directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");

                // Create the directory if it doesn't exist
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                if (imagesToDelete != null)
                {
                    foreach (var imageId in imagesToDelete)
                    {
                        var image = product.ProductImages.FirstOrDefault(pi => pi.ImageID == imageId);
                        if (image != null)
                        {
                            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", image.ImagePath.TrimStart('/'));
                            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                            product.ProductImages.Remove(image);
                        }
                    }
                }

                foreach (var image in newImages)
                {
                    if (image != null)
                    {
                        var fileName = Guid.NewGuid() + Path.GetExtension(image.FileName);
                        var filePath = Path.Combine(directoryPath, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await image.CopyToAsync(stream);
                        }
                        product.ProductImages.Add(new ProductImage { ImagePath = $"{fileName}", IsMainImage = false });
                    }
                }

                if (mainImageId.HasValue)
                {
                    foreach (var image in product.ProductImages)
                    {
                        image.IsMainImage = image.ImageID == mainImageId.Value;
                    }
                }
                else if (product.ProductImages.Any() && !product.ProductImages.Any(i => i.IsMainImage))
                {
                    // If no main image is selected but there are images, set the first one as main
                    product.ProductImages.First().IsMainImage = true;
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật sản phẩm thành công!";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.SubCategories = _context.SubCategories.Include(sc => sc.Category).ToList();
            return View(model);
        }

        // POST: Admin/AdminProduct/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int ProductID)
        {
            var product = _context.Products.Include(p => p.ProductImages).FirstOrDefault(p => p.ProductID == ProductID);
            if (product == null) return NotFound();

            foreach (var image in product.ProductImages)
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", image.ImagePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
            }
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Xóa sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }
    }
}
