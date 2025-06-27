namespace TechWorld.Services
{
    public interface IQrCodeService
    {
        // Trả về chuỗi payload thô
        string GenerateVietQrPayload(decimal amount, string orderInfo);

        // Trả về ảnh base64 từ chuỗi payload
        string GenerateQrImageFromPayload(string payload);

        // Hàm cũ, gọi 2 hàm trên để tương thích
        string GenerateVietQrCodeAsBase64(decimal amount, string orderInfo);
    }
}