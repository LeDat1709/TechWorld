namespace TechWorld.Areas.Admin.Utils
{
    public static class OrderStatusManager
    {
        public const string PendingConfirmation = "Chờ xác nhận";
        public const string AwaitingPayment = "Chờ thanh toán";
        public const string Processing = "Đang xử lý";
        public const string Confirmed = "Đã xác nhận";
        public const string Shipping = "Đang giao hàng";
        public const string Completed = "Đã giao hàng";
        public const string Cancelled = "Đã hủy";

        // Danh sách các trạng thái cho Admin lựa chọn
        public static List<string> AdminStatusList => new List<string>
        {
            PendingConfirmation,
            AwaitingPayment,
            Processing,
            Confirmed,
            Shipping,
            Completed,
            Cancelled
        };
    }
}
