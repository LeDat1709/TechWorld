using Markdig;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TechWorld.Data;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TechWorld.Controllers
{
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public ChatController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public class ChatRequest
        {
            public string userinput { get; set; }
        }

        public class AIResponse
        {
            public Candidate[] candidates { get; set; }

            public class Candidate
            {
                public Content content { get; set; }
            }

            public class Content
            {
                public Part[] parts { get; set; }
            }

            public class Part
            {
                public string text { get; set; }
            }
        }

        [HttpPost]
        public async Task<JsonResult> Chat([FromBody] ChatRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.userinput))
                {
                    return Json(new { success = false, message = "Vui lòng nhập câu hỏi" });
                }

                var systemPrompt = "Bạn là trợ lý của trang web ShopDienTu... " +
                    "Định dạng câu trả lời bằng Markdown... " +
                    "Để chèn ảnh sản phẩm, hãy sử dụng placeholder đặc biệt sau: [IMAGE_FOR:Tên sản phẩm|/images/đường_dẫn_ảnh.jpg]. " + // Hướng dẫn mới
                    "Ví dụ: `Laptop ABC [IMAGE_FOR:Laptop ABC|/images/abc.jpg]`." +
                    "Trả lời thân thiện, chuyên nghiệp, và sử dụng tiếng Việt.";

                var products = _context.Products
                    .Select(p => new
                    {
                        p.ProductName,
                        p.Description,
                        p.Price,
                        p.StockQuantity,
                        SubCategoryName = p.SubCategory != null ? p.SubCategory.SubCategoryName : "N/A",
                        CategoryName = p.SubCategory != null && p.SubCategory.Category != null ? p.SubCategory.Category.CategoryName : "N/A",
                        MainImage = _context.ProductImages
                            .Where(pi => pi.ProductID == p.ProductID && pi.IsMainImage)
                            .Select(pi => pi.ImagePath)
                            .FirstOrDefault()
                    })
                    .ToList();

                var orders = _context.Orders
                    .Select(o => new
                    {
                        o.OrderNumber,
                        o.TotalAmount,
                        o.OrderStatus,
                        o.CreatedAt,
                        UserName = o.User.FullName ?? "Khách vãng lai"
                    })
                    .ToList();

                var orderDetails = _context.OrderDetails
                    .Select(od => new
                    {
                        od.OrderID,
                        od.ProductID,
                        od.Quantity,
                        od.UnitPrice,
                        ProductName = od.Product.ProductName ?? "N/A"
                    })
                    .ToList();

                var promotions = _context.Promotions
                    .Where(p => p.IsActive && p.EndDate > DateTime.Now)
                    .Select(p => new
                    {
                        p.PromotionName,
                        p.PromoCode,
                        p.DiscountPercentage,
                        p.StartDate,
                        p.EndDate,
                        ProductName = p.Product != null ? p.Product.ProductName : "Toàn hệ thống"
                    })
                    .ToList();

                var categories = _context.Categories
                    .Select(c => new { c.CategoryName })
                    .ToList();

                var subcategories = _context.SubCategories
                    .Select(sc => new { sc.SubCategoryName, CategoryName = sc.Category != null ? sc.Category.CategoryName : "N/A" })
                    .ToList();

                var productInfo = string.Join("\n", products.Select(p =>
                    $"- **{p.ProductName}** ({p.CategoryName} - {p.SubCategoryName}): {p.Description} (Giá: *{p.Price:C}*) (Tồn kho: {p.StockQuantity})" +
                    (string.IsNullOrEmpty(p.MainImage) ? "" : $" [IMAGE_FOR:{p.ProductName}|/images/{p.MainImage}]")));
                var promotionInfo = string.Join("\n", promotions.Select(p =>
                    $"- **{p.PromotionName}** ({p.ProductName}): Giảm *{p.DiscountPercentage}%* (Mã: {p.PromoCode}, từ {p.StartDate:dd/MM/yyyy} đến {p.EndDate:dd/MM/yyyy})"));
                var orderInfo = string.Join("\n", orders.Select(o =>
                    $"- **Đơn hàng {o.OrderNumber}**: Tổng tiền *{o.TotalAmount:C}*, Trạng thái: {o.OrderStatus}, Người đặt: {o.UserName}, Ngày: {o.CreatedAt:dd/MM/yyyy}"));
                var orderDetailInfo = string.Join("\n", orderDetails.Select(od =>
                    $"- **Chi tiết đơn {od.OrderID}**: Sản phẩm {od.ProductName}, Số lượng: {od.Quantity}, Đơn giá: *{od.UnitPrice:C}*"));

                var fullContext = $"Danh mục: {string.Join(", ", categories.Select(c => c.CategoryName))}\n" +
                    $"Danh mục phụ: {string.Join(", ", subcategories.Select(sc => $"{sc.SubCategoryName} ({sc.CategoryName})"))}\n" +
                    $"Sản phẩm:\n{productInfo}\n" +
                    $"Đơn hàng:\n{orderInfo}\n" +
                    $"Chi tiết đơn hàng:\n{orderDetailInfo}\n" +
                    $"Khuyến mãi:\n{promotionInfo}";

                var fullPrompt = $"{systemPrompt}\n\nThông tin:\n{fullContext}\n\nCâu hỏi: {request.userinput}";

                using var httpClient = new HttpClient();
                var url = $"{_configuration["AISettings:ApiUrl"]}?key={_configuration["AISettings:ApiKey"]}";
                if (string.IsNullOrEmpty(url))
                {
                    return Json(new { success = false, message = "API URL không được cấu hình trong appsettings.json" });
                }

                var data = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = fullPrompt }
                            }
                        }
                    }
                };
                var response = await httpClient.PostAsJsonAsync(url, data);

                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = $"Lỗi kết nối với máy chủ AI: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}" });
                }

                var result = await response.Content.ReadFromJsonAsync<AIResponse>();

                if (result == null || result.candidates == null || result.candidates.Length == 0 || result.candidates[0].content == null || result.candidates[0].content.parts == null || result.candidates[0].content.parts.Length == 0)
                {
                    return Json(new { success = false, message = "Phản hồi từ AI không hợp lệ" });
                }

                string aiResponseText = result.candidates[0].content.parts[0].text;

                string processedResponse = Regex.Replace(aiResponseText, @"\[IMAGE_FOR:(.*?)\|(.*?)\]",
                    match => $"![{match.Groups[1].Value.Trim()}]({match.Groups[2].Value.Trim()})");

                string htmlResponse = Markdown.ToHtml(processedResponse);

                return Json(new { success = true, html = htmlResponse });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
    }
}