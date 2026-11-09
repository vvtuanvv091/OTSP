namespace Backend_Project_OTSP.Services.Payments
{
    public class VietQrService
    {
        private readonly string _bankId;
        private readonly string _accountNo;
        private readonly string _accountName;
        private readonly string _template;


        public VietQrService(IConfiguration config)
        {
            _bankId = config["VietQR:BankId"] ?? "970422";
            _accountNo = config["VietQR:AccountNo"] ?? "0984391940";
            _accountName = config["VietQR:AccountName"] ?? "TONG NGOC TUAN";
            _template = config["VietQR:Template"] ?? "compact";
        }
        public string GetQrImageUrl(long amount, string description)
        {
            // VietQR Quick Link API � ho�n to�n mi?n ph�
            // T�i li?u: https://www.vietqr.io/danh-sach-api/api-tao-ma-qr/
            var encodedDesc = Uri.EscapeDataString(description);
            var encodedName = Uri.EscapeDataString(_accountName);

            return $"https://img.vietqr.io/image/{_bankId}-{_accountNo}-{_template}.png" +
                   $"?amount={amount}" +
                   $"&addInfo={encodedDesc}" +
                   $"&accountName={encodedName}";
        }
        public string GenerateQrData(long amount, string description, string orderCode)
        {
            // �on gi?n: tr? v? URL VietQR (cung l� QR data h?p l?)
            return GetQrImageUrl(amount, description);
        }
        public string GetQrImageUrl(string amount, string description)
            => GetQrImageUrl(long.Parse(amount), description);
    }

}
