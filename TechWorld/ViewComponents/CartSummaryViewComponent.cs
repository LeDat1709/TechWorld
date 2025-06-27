using TechWorld.Models;
using Microsoft.AspNetCore.Mvc;
using TechWorld.Services;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Security.Claims;

namespace TechWorld.ViewComponents
{
    public class CartSummaryViewComponent : ViewComponent
    {
        private readonly IShoppingCartService _shoppingCartService;

        public CartSummaryViewComponent(IShoppingCartService shoppingCartService)
        {
            _shoppingCartService = shoppingCartService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var cart = await _shoppingCartService.GetCartAsync(User as ClaimsPrincipal, HttpContext.Session);

            // Trả về tổng số lượng sản phẩm từ giỏ hàng (View Model)
            return Content(cart.GetTotalItems().ToString());
        }
    }
}
