using TechWorld.Models.ViewModels;

namespace TechWorld.Models
{
    public class ShoppingCart
    {
        public List<CartItemViewModel> Items { get; set; } = new List<CartItemViewModel>();

        public void AddItem(Product product, int quantity)
        {
            var existingItem = Items.FirstOrDefault(i => i.ProductID == product.ProductID);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                Items.Add(new CartItemViewModel
                {
                    ProductID = product.ProductID,
                    ProductName = product.ProductName,
                    Price = product.Price,
                    Quantity = quantity,
                    ImagePath = product.ProductImages?.FirstOrDefault(i => i.IsMainImage)?.ImagePath ?? "/images/no-image.jpg"
                });
            }
        }

        public void RemoveItem(int productId)
        {
            var item = Items.FirstOrDefault(i => i.ProductID == productId);
            if (item != null)
            {
                Items.Remove(item);
            }
        }

        public void UpdateQuantity(int productId, int quantity)
        {
            var item = Items.FirstOrDefault(i => i.ProductID == productId);
            if (item != null)
            {
                item.Quantity = quantity;
            }
        }

        public void Clear()
        {
            Items.Clear();
        }

        public decimal GetTotal()
        {
            return Items.Sum(i => i.Price * i.Quantity);
        }

        public int GetTotalItems()
        {
            return Items.Sum(i => i.Quantity);
        }
    }
}
