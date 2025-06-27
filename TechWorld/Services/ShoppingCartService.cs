using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TechWorld.Data;
using TechWorld.Models; // Cần thiết cho Cart, CartItem (DB Entities) và Product, User, Promotion, Rank
using TechWorld.Models.ViewModels; // Cần thiết cho CartItemViewModel
using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http; // Cho ISession
using System; // Cho DateTime và Exception
using System.Collections.Generic;
using TechWorld.Services; // Cho List

namespace TechWorld.Services
{
    public class ShoppingCartService : IShoppingCartService
    {
        private readonly ApplicationDbContext _context;
        private const string CartSessionKey = "Cart";

        public ShoppingCartService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ShoppingCart> GetCartAsync(ClaimsPrincipal user, ISession session)
        {
            var shoppingCart = new ShoppingCart(); // Đây là View Model ShoppingCart của bạn

            if (user.Identity.IsAuthenticated)
            {
                var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier));
                var dbCart = await _context.Carts
                    .Include(c => c.CartItems) // Include DB items (CartItem entity)
                        .ThenInclude(ci => ci.Product) // Include Product info
                            .ThenInclude(p => p.ProductImages) // Include Product Images
                    .FirstOrDefaultAsync(c => c.UserID == userId);

                if (dbCart == null)
                {
                    // Tạo một giỏ hàng mới cho người dùng nếu chưa tồn tại
                    dbCart = new Cart { UserID = userId, CreatedAt = DateTime.Now, LastUpdatedAt = DateTime.Now };
                    _context.Carts.Add(dbCart);
                    await _context.SaveChangesAsync(); // Lưu để có CartID
                }

                // Map các mục giỏ hàng từ DB Entity (CartItem) sang View Model (CartItemViewModel)
                foreach (var dbItem in dbCart.CartItems)
                {
                    shoppingCart.Items.Add(new CartItemViewModel
                    {
                        ProductID = dbItem.ProductID,
                        ProductName = dbItem.Product?.ProductName, // Lấy từ Product
                        Price = dbItem.UnitPriceAtAddition, // Quan trọng: Sử dụng giá đã tính toán khi thêm vào giỏ
                        Quantity = dbItem.Quantity,
                        ImagePath = dbItem.Product?.ProductImages?.FirstOrDefault(i => i.IsMainImage)?.ImagePath ?? "/images/no-image.jpg"
                    });
                }
            }
            else
            {
                // Đối với người dùng ẩn danh, lấy giỏ hàng từ Session
                shoppingCart = GetCartFromSessionInternal(session);

                // Cập nhật giá/tên sản phẩm cho giỏ hàng ẩn danh từ DB để đảm bảo thông tin luôn mới nhất
                foreach (var itemVm in shoppingCart.Items)
                {
                    var product = await _context.Products
                        .Include(p => p.ProductImages)
                        .FirstOrDefaultAsync(p => p.ProductID == itemVm.ProductID);
                    if (product != null)
                    {
                        itemVm.ProductName = product.ProductName;
                        decimal productSpecificDiscountPercentage = 0m;
                        var now = DateTime.Now;
                        var promo = await _context.Promotions
                            .Where(p => p.ProductID == itemVm.ProductID && p.IsActive && p.StartDate <= now && p.EndDate >= now)
                            .FirstOrDefaultAsync();
                        if (promo != null)
                        {
                            productSpecificDiscountPercentage = promo.DiscountPercentage;
                        }
                        itemVm.Price = product.Price * (1 - productSpecificDiscountPercentage / 100m);
                        itemVm.ImagePath = product.ProductImages?.FirstOrDefault(i => i.IsMainImage)?.ImagePath ?? "/images/no-image.jpg";
                    }
                }
            }
            return shoppingCart;
        }

        public async Task AddItemAsync(ClaimsPrincipal user, ISession session, int productId, int quantity)
        {
            var product = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductID == productId);

            if (product == null)
            {
                throw new Exception("Product not found.");
            }

            if (quantity <= 0)
            {
                throw new Exception("Số lượng sản phẩm không hợp lệ.");
            }

            if (user.Identity.IsAuthenticated)
            {
                var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier));
                var dbCart = await _context.Carts
                    .Include(c => c.CartItems)
                    .FirstOrDefaultAsync(c => c.UserID == userId);

                if (dbCart == null)
                {
                    dbCart = new Cart { UserID = userId, CreatedAt = DateTime.Now, LastUpdatedAt = DateTime.Now };
                    _context.Carts.Add(dbCart);
                    await _context.SaveChangesAsync();
                }

                var existingCartItem = dbCart.CartItems.FirstOrDefault(ci => ci.ProductID == productId); // DB entity CartItem

                decimal productSpecificDiscountPercentage = 0m;
                var now = DateTime.Now;
                var promo = await _context.Promotions
                    .Where(p => p.ProductID == productId && p.IsActive && p.StartDate <= now && p.EndDate >= now)
                    .FirstOrDefaultAsync();

                if (promo != null)
                {
                    productSpecificDiscountPercentage = promo.DiscountPercentage;
                }

                decimal rankDiscountPercentage = 0m;
                var appUser = await _context.Users
                    .Include(u => u.Rank)
                    .FirstOrDefaultAsync(u => u.UserID == userId);

                if (appUser != null && appUser.Rank != null)
                {
                    rankDiscountPercentage = appUser.Rank.DiscountPercentage;
                }

                decimal effectiveDiscountPercentage = Math.Max(productSpecificDiscountPercentage, rankDiscountPercentage);
                decimal finalUnitPrice = product.Price * (1 - effectiveDiscountPercentage / 100m);

                if (existingCartItem != null)
                {
                    if (existingCartItem.Quantity + quantity > product.StockQuantity)
                    {
                        throw new Exception($"Số lượng yêu cầu vượt quá tồn kho. Chỉ còn {product.StockQuantity - existingCartItem.Quantity} sản phẩm có thể thêm.");
                    }
                    existingCartItem.Quantity += quantity;
                    existingCartItem.UnitPriceAtAddition = finalUnitPrice;
                    existingCartItem.DiscountPercentageAtAddition = effectiveDiscountPercentage;
                }
                else
                {
                    if (quantity > product.StockQuantity)
                    {
                        throw new Exception($"Số lượng yêu cầu vượt quá tồn kho. Chỉ còn {product.StockQuantity} sản phẩm trong kho.");
                    }
                    dbCart.CartItems.Add(new CartItem // DB entity CartItem
                    {
                        ProductID = productId,
                        Quantity = quantity,
                        UnitPriceAtAddition = finalUnitPrice,
                        DiscountPercentageAtAddition = effectiveDiscountPercentage
                    });
                }
                dbCart.LastUpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }
            else
            {
                var shoppingCart = GetCartFromSessionInternal(session);

                decimal productSpecificDiscountPercentage = 0m;
                var now = DateTime.Now;
                var promo = await _context.Promotions
                    .Where(p => p.ProductID == productId && p.IsActive && p.StartDate <= now && p.EndDate >= now)
                    .FirstOrDefaultAsync();

                if (promo != null)
                {
                    productSpecificDiscountPercentage = promo.DiscountPercentage;
                }
                decimal priceForSessionCart = product.Price * (1 - productSpecificDiscountPercentage / 100m);

                var existingSessionItem = shoppingCart.Items.FirstOrDefault(item => item.ProductID == productId);
                if (existingSessionItem != null)
                {
                    if (existingSessionItem.Quantity + quantity > product.StockQuantity)
                    {
                        throw new Exception($"Số lượng yêu cầu vượt quá tồn kho. Chỉ còn {product.StockQuantity - existingSessionItem.Quantity} sản phẩm có thể thêm.");
                    }
                }
                else
                {
                    if (quantity > product.StockQuantity)
                    {
                        throw new Exception($"Số lượng yêu cầu vượt quá tồn kho. Chỉ còn {product.StockQuantity} sản phẩm trong kho.");
                    }
                }

                var tempProduct = new Product
                {
                    ProductID = product.ProductID,
                    ProductName = product.ProductName,
                    Price = priceForSessionCart,
                    ProductImages = product.ProductImages
                };
                shoppingCart.AddItem(tempProduct, quantity);
                SaveCartToSessionInternal(session, shoppingCart);
            }
        }

        public async Task RemoveItemAsync(ClaimsPrincipal user, ISession session, int productId)
        {
            if (user.Identity.IsAuthenticated)
            {
                var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier));
                var dbCart = await _context.Carts
                    .Include(c => c.CartItems)
                    .FirstOrDefaultAsync(c => c.UserID == userId);

                if (dbCart != null)
                {
                    var itemToRemove = dbCart.CartItems.FirstOrDefault(ci => ci.ProductID == productId); // DB entity CartItem
                    if (itemToRemove != null)
                    {
                        _context.CartItems.Remove(itemToRemove);
                        dbCart.LastUpdatedAt = DateTime.Now;
                        await _context.SaveChangesAsync();
                    }
                }
            }
            else
            {
                var shoppingCart = GetCartFromSessionInternal(session);
                shoppingCart.RemoveItem(productId);
                SaveCartToSessionInternal(session, shoppingCart);
            }
        }

        public async Task UpdateQuantityAsync(ClaimsPrincipal user, ISession session, int productId, int quantity)
        {
            if (quantity <= 0)
            {
                await RemoveItemAsync(user, session, productId);
                return;
            }

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == productId);
            if (product == null)
            {
                throw new Exception("Product not found.");
            }

            if (user.Identity.IsAuthenticated)
            {
                var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier));
                var dbCart = await _context.Carts
                    .Include(c => c.CartItems)
                    .FirstOrDefaultAsync(c => c.UserID == userId);

                if (dbCart != null)
                {
                    var existingCartItem = dbCart.CartItems.FirstOrDefault(ci => ci.ProductID == productId); // DB entity CartItem
                    if (existingCartItem != null)
                    {
                        if (quantity > product.StockQuantity)
                        {
                            throw new Exception($"Số lượng yêu cầu vượt quá tồn kho. Chỉ còn {product.StockQuantity} sản phẩm trong kho.");
                        }
                        existingCartItem.Quantity = quantity;
                        dbCart.LastUpdatedAt = DateTime.Now;
                        await _context.SaveChangesAsync();
                    }
                }
            }
            else
            {
                var shoppingCart = GetCartFromSessionInternal(session);
                var existingSessionItem = shoppingCart.Items.FirstOrDefault(item => item.ProductID == productId);
                if (existingSessionItem != null)
                {
                    if (quantity > product.StockQuantity)
                    {
                        throw new Exception($"Số lượng yêu cầu vượt quá tồn kho. Chỉ còn {product.StockQuantity} sản phẩm trong kho.");
                    }
                }
                shoppingCart.UpdateQuantity(productId, quantity);
                SaveCartToSessionInternal(session, shoppingCart);
            }
        }

        public async Task ClearCartAsync(ClaimsPrincipal user, ISession session)
        {
            if (user.Identity.IsAuthenticated)
            {
                var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier));
                var dbCart = await _context.Carts
                    .Include(c => c.CartItems)
                    .FirstOrDefaultAsync(c => c.UserID == userId);

                if (dbCart != null)
                {
                    _context.CartItems.RemoveRange(dbCart.CartItems); // Xóa DB entity CartItem
                    dbCart.LastUpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                session.Remove(CartSessionKey);
            }
        }

        public async Task MergeAnonymousCartAsync(ClaimsPrincipal user, ISession session)
        {
            if (!user.Identity.IsAuthenticated) return;

            var anonymousCart = GetCartFromSessionInternal(session);
            if (anonymousCart == null || !anonymousCart.Items.Any()) return;

            var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier));
            var userDbCart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UserID == userId);

            if (userDbCart == null)
            {
                userDbCart = new Cart { UserID = userId, CreatedAt = DateTime.Now, LastUpdatedAt = DateTime.Now };
                _context.Carts.Add(userDbCart);
                await _context.SaveChangesAsync();
            }

            foreach (var anonymousItem in anonymousCart.Items) // CartItemViewModel
            {
                var existingDbItem = userDbCart.CartItems.FirstOrDefault(ci => ci.ProductID == anonymousItem.ProductID); // DB entity CartItem
                var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == anonymousItem.ProductID);

                if (product == null) continue;

                // Recalculate price with user-specific discounts upon merging
                decimal productSpecificDiscountPercentage = 0m;
                var now = DateTime.Now;
                var promo = await _context.Promotions
                    .Where(p => p.ProductID == anonymousItem.ProductID && p.IsActive && p.StartDate <= now && p.EndDate >= now)
                    .FirstOrDefaultAsync();
                if (promo != null) productSpecificDiscountPercentage = promo.DiscountPercentage;

                decimal rankDiscountPercentage = 0m;
                var appUser = await _context.Users
                    .Include(u => u.Rank)
                    .FirstOrDefaultAsync(u => u.UserID == userId);
                if (appUser != null && appUser.Rank != null) rankDiscountPercentage = appUser.Rank.DiscountPercentage;

                decimal effectiveDiscountPercentage = Math.Max(productSpecificDiscountPercentage, rankDiscountPercentage);
                decimal finalUnitPrice = product.Price * (1 - effectiveDiscountPercentage / 100m);


                if (existingDbItem != null)
                {
                    int newQuantity = existingDbItem.Quantity + anonymousItem.Quantity;
                    existingDbItem.Quantity = Math.Min(newQuantity, product.StockQuantity);
                    existingDbItem.UnitPriceAtAddition = finalUnitPrice;
                    existingDbItem.DiscountPercentageAtAddition = effectiveDiscountPercentage;
                }
                else
                {
                    userDbCart.CartItems.Add(new CartItem // DB entity CartItem
                    {
                        ProductID = anonymousItem.ProductID,
                        Quantity = Math.Min(anonymousItem.Quantity, product.StockQuantity),
                        UnitPriceAtAddition = finalUnitPrice,
                        DiscountPercentageAtAddition = effectiveDiscountPercentage
                    });
                }
            }
            userDbCart.LastUpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            session.Remove(CartSessionKey); // Xóa giỏ hàng trong session sau khi hợp nhất
        }

        // --- Helper methods cho giỏ hàng trong Session (chỉ cho người dùng ẩn danh) ---
        private ShoppingCart GetCartFromSessionInternal(ISession session)
        {
            var cartJson = session.GetString(CartSessionKey);
            return string.IsNullOrEmpty(cartJson) ? new ShoppingCart() : JsonConvert.DeserializeObject<ShoppingCart>(cartJson);
        }

        private void SaveCartToSessionInternal(ISession session, ShoppingCart cart)
        {
            var cartJson = JsonConvert.SerializeObject(cart);
            session.SetString(CartSessionKey, cartJson);
        }
    }
}