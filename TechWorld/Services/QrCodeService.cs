using Microsoft.Extensions.Options;
using TechWorld.Settings;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using QRCoder;

namespace TechWorld.Services
{
    public class QrCodeService : IQrCodeService
    {
        private readonly BankInfoSettings _bankInfo;

        public QrCodeService(IOptions<BankInfoSettings> bankInfoOptions)
        {
            _bankInfo = bankInfoOptions.Value;
        }

        // Hàm chính để tạo QR Code, gọi 2 hàm con bên dưới
        public string GenerateVietQrCodeAsBase64(decimal amount, string orderInfo)
        {
            string payload = GenerateVietQrPayload(amount, orderInfo);
            return GenerateQrImageFromPayload(payload);
        }

        // Hàm TẠO CHUỖI PAYLOAD
        public string GenerateVietQrPayload(decimal amount, string orderInfo)
        {
            var merchantInfo = new StringBuilder();
            merchantInfo.Append(BuildTLV("00", "A000000727"));
            var beneficiary = new StringBuilder();
            beneficiary.Append(BuildTLV("00", _bankInfo.BankId));
            beneficiary.Append(BuildTLV("01", _bankInfo.AccountNumber));
            merchantInfo.Append(BuildTLV("01", beneficiary.ToString()));

            string sanitizedOrderInfo = SanitizeString(orderInfo);
            var additionalData = new StringBuilder();
            additionalData.Append(BuildTLV("08", sanitizedOrderInfo));

            var payload = new StringBuilder();
            payload.Append(BuildTLV("00", "01"));
            payload.Append(BuildTLV("01", "12"));
            payload.Append(BuildTLV("38", merchantInfo.ToString()));
            payload.Append(BuildTLV("53", "704"));
            payload.Append(BuildTLV("54", amount.ToString("F0"))); // Giá trị Amount không cần sanitize
            payload.Append(BuildTLV("58", "VN"));

            if (!string.IsNullOrEmpty(_bankInfo.AccountName))
            {
                payload.Append(BuildTLV("59", SanitizeString(_bankInfo.AccountName)));
            }

            payload.Append(BuildTLV("62", additionalData.ToString()));

            string payloadToHash = payload.ToString() + "6304";
            string crc = CalculateCrc16(payloadToHash);
            payload.Append("6304" + crc);

            return payload.ToString();
        }

        // Hàm TẠO ẢNH QR từ một chuỗi payload đã có
        public string GenerateQrImageFromPayload(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return null;

            using (var qrGenerator = new QRCodeGenerator())
            using (var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q))
            using (var qrCode = new Base64QRCode(qrCodeData))
            {
                return "data:image/png;base64," + qrCode.GetGraphic(20);
            }
        }

        private string BuildTLV(string tag, string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            int length = Encoding.UTF8.GetByteCount(value);
            return $"{tag}{length:D2}{value}";
        }

        private string SanitizeString(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var normalizedString = input.ToUpper().Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();
            foreach (var c in normalizedString)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }
            string result = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
            // Chỉ giữ lại ký tự A-Z, 0-9 và loại bỏ các ký tự khác bao gồm cả khoảng trắng
            result = Regex.Replace(result, @"[^A-Z0-9]", "");
            return result;
        }

        // HÀM ĐÃ SỬA LỖI - DÙNG Encoding.UTF8
        private string CalculateCrc16(string data)
        {
            // Sử dụng UTF-8 để nhất quán với việc tính toán độ dài trong BuildTLV
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            ushort crc = 0xFFFF;
            foreach (byte b in bytes)
            {
                crc ^= (ushort)(b << 8);
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    else
                        crc <<= 1;
                }
            }
            return crc.ToString("X4");
        }
    }
}