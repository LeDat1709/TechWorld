namespace TechWorld.Models.ViewModels
{
    public class CartItemViewModel
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImagePath { get; set; }
        public decimal Total => Price * Quantity;
    }
    
}
