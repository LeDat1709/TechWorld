using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TechWorld.Data;
using TechWorld.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace TechWorld.Controllers
{
    public class ProductWithPrice
    {
        public Product Product { get; set; }
        public decimal EffectivePrice { get; set; }
    }
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        private IQueryable<ProductWithPrice> GetProductsWithCalculatedPrice(IQueryable<Product> sourceQuery, decimal rankDiscountPercentage)
        {
            var now = DateTime.Now;

            return sourceQuery
                .Select(p => new
                {
                    Product = p,
                    BestPromotion = _context.Promotions
                        .Where(promo => promo.ProductID == p.ProductID &&
                                        promo.IsActive &&
                                        promo.StartDate <= now &&
                                        promo.EndDate >= now)
                        .OrderByDescending(promo => promo.DiscountPercentage)
                        .FirstOrDefault()
                })
                .Select(x => new ProductWithPrice
                {
                    Product = x.Product,
                    EffectivePrice = (x.Product.Price - (x.BestPromotion != null ? x.Product.Price * (x.BestPromotion.DiscountPercentage / 100M) : 0m)) * (1 - rankDiscountPercentage / 100m)
                });
        }

        public async Task<IActionResult> Index(string searchTerm, int? categoryId = null, int? subcategoryId = null, string sortOrder = null, int? page = 1, int? pageSize = 10, decimal? minPrice = null, decimal? maxPrice = null, int? suggestedPage = 1, int? newestPage = 1)
        {
            var now = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string currentSearchHistory = Request.Cookies["SearchHistory"] ?? "";
                List<string> searchTerms = currentSearchHistory.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();

                searchTerms.RemoveAll(s => s.Equals(searchTerm.Trim(), StringComparison.OrdinalIgnoreCase));
                searchTerms.Insert(0, searchTerm.Trim());

                if (searchTerms.Count > 5) searchTerms = searchTerms.Take(5).ToList();

                var cookieOptions = new CookieOptions { Expires = DateTime.Now.AddDays(30), HttpOnly = true, IsEssential = true };
                Response.Cookies.Append("SearchHistory", string.Join("|", searchTerms), cookieOptions);
            }
            // L?y t?t c? các khuy?n mãi ?ang ho?t ??ng (nh? ban ??u, ?? hi?n th? chung n?u c?n)
            var activePromotions = await _context.Promotions
                .Where(p => p.IsActive && p.ProductID != null && p.StartDate <= now && p.EndDate >= now)
                .ToListAsync();
            ViewBag.ActivePromotions = activePromotions; // Gi? l?i ViewBag này

            // L?y chi?t kh?u rank c?a ng??i dùng hi?n t?i
            decimal rankDiscountPercentage = 0m;
            if (User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdClaim, out int userId))
                {
                    var user = await _context.Users
                        .Include(u => u.Rank)
                        .FirstOrDefaultAsync(u => u.UserID == userId);

                    if (user != null && user.Rank != null)
                    {
                        rankDiscountPercentage = user.Rank.DiscountPercentage;
                    }
                }
            }
            ViewBag.RankDiscountPercentage = rankDiscountPercentage;

            // L?y t?t c? danh m?c và danh m?c con cho sidebar
            var categories = await _context.Categories
                .Include(c => c.SubCategories)
                .ToListAsync();
            ViewBag.Categories = categories; // Gi? l?i ViewBag này

            // ====== LOGIC S?N PH?M M?I NH?T =====
            const int maxTotalNewest = 10;
            const int newestItemsPerPage = 5;
            int currentNewestPage = newestPage ?? 1;

            var baseNewestQuery = _context.Products
                                          .Include(p => p.SubCategory)
                                          .Include(p => p.ProductImages)
                                          .Where(p => p.IsActive);

            var newestProductsWithPrice = GetProductsWithCalculatedPrice(baseNewestQuery, rankDiscountPercentage);

            // Áp d?ng b? l?c cho s?n ph?m m?i
            if (categoryId.HasValue)
            {
                newestProductsWithPrice = newestProductsWithPrice.Where(x => x.Product.SubCategory.CategoryID == categoryId.Value);
            }
            if (subcategoryId.HasValue) { newestProductsWithPrice = newestProductsWithPrice.Where(x => x.Product.SubCategoryID == subcategoryId.Value); }
            if (minPrice.HasValue) { newestProductsWithPrice = newestProductsWithPrice.Where(x => x.EffectivePrice >= minPrice.Value); }
            if (maxPrice.HasValue) { newestProductsWithPrice = newestProductsWithPrice.Where(x => x.EffectivePrice <= maxPrice.Value); }

            var allNewestProducts = await newestProductsWithPrice
                .OrderByDescending(x => x.Product.CreatedAt)
                .Take(maxTotalNewest)
                .Select(x => x.Product) // Bây gi? ch? c?n Select, không Include n?a
                .ToListAsync();

            var pagedNewestProducts = allNewestProducts
                .Skip((currentNewestPage - 1) * newestItemsPerPage)
                .Take(newestItemsPerPage)
                .ToList();

            ViewBag.NewestProducts = pagedNewestProducts;
            ViewBag.CurrentNewestPage = currentNewestPage;
            ViewBag.TotalNewestPages = (int)Math.Ceiling((double)allNewestProducts.Count / newestItemsPerPage);

            // ===== LOGIC G?I Ý S?N PH?M =====

            const int maxTotalSuggested = 10;
            const int suggestedItemsPerPage = 5;
            int currentSuggestedPage = suggestedPage ?? 1;

            string searchHistoryCookie = Request.Cookies["SearchHistory"] ?? "";
            string viewedProductsCookie = Request.Cookies["ViewedProducts"] ?? "";
            var recentSearchTerms = searchHistoryCookie.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();
            var viewedProductIds = viewedProductsCookie.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                       .Select(idStr => int.TryParse(idStr, out int id) ? id : (int?)null)
                                                       .Where(id => id.HasValue).Select(id => id.Value).ToList();

            var subCategoryIdsFromHistory = new List<int?>();
            //if (viewedProductIds.Any())
            //{
            //    subCategoryIdsFromHistory = await _context.Products
            //        .Where(p => viewedProductIds.Contains(p.ProductID))
            //        .Select(p => p.SubCategoryID)
            //        .Distinct()
            //        .ToListAsync();
            //}

            IQueryable<Product> combinedSuggestedQuery;
            if (recentSearchTerms.Any() || subCategoryIdsFromHistory.Any())
            {
                combinedSuggestedQuery = _context.Products
                    .Where(p => p.IsActive && (recentSearchTerms.Any(term => p.ProductName.Contains(term)) || subCategoryIdsFromHistory.Contains(p.SubCategoryID)));
            }
            else
            {
                combinedSuggestedQuery = _context.Products.Where(p => p.IsActive);
            }

            combinedSuggestedQuery = combinedSuggestedQuery
                                        .Include(p => p.SubCategory)
                                        .Include(p => p.ProductImages);

            var suggestedProductsWithPrice = GetProductsWithCalculatedPrice(combinedSuggestedQuery, rankDiscountPercentage);

            if (categoryId.HasValue)
            {
                suggestedProductsWithPrice = suggestedProductsWithPrice.Where(x => x.Product.SubCategory.CategoryID == categoryId.Value);
            }
            if (subcategoryId.HasValue)
            {
                suggestedProductsWithPrice = suggestedProductsWithPrice.Where(x => x.Product.SubCategoryID == subcategoryId.Value);
            }
            if (minPrice.HasValue)
            {
                suggestedProductsWithPrice = suggestedProductsWithPrice.Where(x => x.EffectivePrice >= minPrice.Value);
            }
            if (maxPrice.HasValue)
            {
                suggestedProductsWithPrice = suggestedProductsWithPrice.Where(x => x.EffectivePrice <= maxPrice.Value);
            }

            var allSuggestedProducts = await suggestedProductsWithPrice
                .OrderByDescending(x => x.Product.ProductID)
                .Take(maxTotalSuggested)
                .Select(x => x.Product) // Ch? Select, không Include
                .ToListAsync();

            allSuggestedProducts = allSuggestedProducts.DistinctBy(p => p.ProductID).ToList();

            int totalSuggestedPages = (int)Math.Ceiling((double)allSuggestedProducts.Count / suggestedItemsPerPage);
            var pagedSuggestedProducts = allSuggestedProducts
                .Skip((currentSuggestedPage - 1) * suggestedItemsPerPage)
                .Take(suggestedItemsPerPage)
                .ToList();

            ViewBag.SuggestedProducts = pagedSuggestedProducts;
            ViewBag.CurrentSuggestedPage = currentSuggestedPage;
            ViewBag.TotalSuggestedPages = totalSuggestedPages;

            // ===== LOGIC S?N PH?M CHÍNH =====
            // S? d?ng m?t ki?u ?n danh ?? ch?a Product và EffectivePrice ?ã tính toán
            var baseProductsQuery = _context.Products
                .Include(p => p.SubCategory.Category)
                .Include(p => p.ProductImages) // Thêm Include ? ?ây
                .Where(p => p.IsActive);

            var finalProductsQuery = GetProductsWithCalculatedPrice(baseProductsQuery, rankDiscountPercentage);

            // L?c theo t? khóa tìm ki?m
            if (!string.IsNullOrEmpty(searchTerm))
            {
                finalProductsQuery = finalProductsQuery.Where(x =>
                    x.Product.ProductName.ToLower().Contains(searchTerm.ToLower()) ||
                    (x.Product.Description != null && x.Product.Description.ToLower().Contains(searchTerm.ToLower())));
                ViewBag.SearchTerm = searchTerm;
            }

            if (categoryId.HasValue)
            {
                finalProductsQuery = finalProductsQuery.Where(x => x.Product.SubCategory.CategoryID == categoryId.Value);
                ViewBag.SelectedCategoryId = categoryId.Value;
            }

            if (subcategoryId.HasValue)
            {
                finalProductsQuery = finalProductsQuery.Where(x => x.Product.SubCategoryID == subcategoryId.Value);
                ViewBag.SelectedSubcategoryId = subcategoryId.Value;
            }
            if (minPrice.HasValue)
            {
                finalProductsQuery = finalProductsQuery.Where(x => x.EffectivePrice >= minPrice.Value);
            }
            if (maxPrice.HasValue)
            {
                finalProductsQuery = finalProductsQuery.Where(x => x.EffectivePrice <= maxPrice.Value);
            }
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;

            // Áp d?ng s?p x?p
            switch (sortOrder)
            {
                case "price_asc":
                    finalProductsQuery = finalProductsQuery.OrderBy(x => x.EffectivePrice);
                    break;
                case "price_desc":
                    finalProductsQuery = finalProductsQuery.OrderByDescending(x => x.EffectivePrice);
                    break;
                case "newest":
                    finalProductsQuery = finalProductsQuery.OrderByDescending(x => x.Product.CreatedAt);
                    break;
                default:
                    // Gi? l?i s?p x?p m?c ??nh n?u có tìm ki?m ho?c ?? nh? c?
                    if (string.IsNullOrEmpty(searchTerm))
                    {
                        finalProductsQuery = finalProductsQuery.OrderByDescending(x => x.Product.CreatedAt);
                    }
                    break;
            }
            ViewBag.CurrentSort = sortOrder;

            int currentPage = page ?? 1;
            int itemsPerPage = pageSize ?? 10;
            int totalItems = await finalProductsQuery.CountAsync();

            // L?y d? li?u phân trang, không c?n .Include ? cu?i n?a
            var products = await finalProductsQuery
                .Skip((currentPage - 1) * itemsPerPage)
                .Take(itemsPerPage)
                .Select(x => x.Product)
                .AsSplitQuery()
                .ToListAsync();

            ViewBag.TotalItems = totalItems;

            int totalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage);
            ViewBag.TotalPages = totalPages;
            ViewBag.CurrentPage = currentPage;
            ViewBag.PageSize = itemsPerPage;

            return View(products);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    public class ErrorViewModel
    {
        public string RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
