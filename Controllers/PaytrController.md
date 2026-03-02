[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _clientFactory;

    public PaymentController(IConfiguration config, IHttpClientFactory clientFactory)
    {
        _config = config;
        _clientFactory = clientFactory;
    }

    [HttpPost("get-token")]
    public async Task<IActionResult> GetToken([FromBody] PaymentRequest model)
    {
        // 1. PayTR Bilgilerini Al
        var settings = _config.GetSection("PayTR");
        string merchantId = settings["MerchantId"];
        string merchantKey = settings["MerchantKey"];
        string merchantSalt = settings["MerchantSalt"];

        // 2. Sipariş Bilgilerini Hazırla
        string merchantOid = "FGS" + DateTime.Now.Ticks; // Benzersiz sipariş ID
        string userIp = Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        int paymentAmount = (int)(model.Price * 100); // Kuruş cinsinden

        // Sepet İçeriği (Yeni sistemde paket ismini dinamik alalım)
        var basket = new object[][] { new object[] { model.PackageName, paymentAmount.ToString(), 1 } };
        string userBasket = JsonConvert.SerializeObject(basket);

        // 3. Hash Oluşturma (PayTR Standart Sıralaması)
        string hashStr = merchantId + userIp + merchantOid + model.Email + paymentAmount + userBasket + "0" + "0" + "TL" + settings["TestMode"];
        string token = "";
        using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(merchantKey)))
        {
            byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(hashStr + merchantSalt));
            token = Convert.ToBase64String(hashBytes);
        }

        // 4. PayTR'a İstek At
        var values = new Dictionary<string, string>
        {
            { "merchant_id", merchantId },
            { "user_ip", userIp },
            { "merchant_oid", merchantOid },
            { "email", model.Email },
            { "payment_amount", paymentAmount.ToString() },
            { "paytr_token", token },
            { "user_basket", userBasket },
            { "debug_on", settings["DebugOn"] },
            { "no_installment", "0" },
            { "max_installment", "0" },
            { "user_name", model.FullName },
            { "user_address", "FGS Trade Digital Service" }, // Dijital ürün olduğu için sabit kalabilir
            { "user_phone", model.Phone },
            { "merchant_ok_url", "https://fgstrade.com/payment-success" },
            { "merchant_fail_url", "https://fgstrade.com/payment-failed" },
            { "merchant_notify_url", settings["CallbackUrl"] },
            { "currency", "TL" },
            { "test_mode", settings["TestMode"] }
        };

        var client = _clientFactory.CreateClient();
        var response = await client.PostAsync("https://www.paytr.com/odeme/api/get-token", new FormUrlEncodedContent(values));
        var result = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());

        if (result.status == "success")
            return Ok(new { token = result.token, iframeUrl = $"https://www.paytr.com/odeme/guvenli/{result.token}" });

        return BadRequest(result);
    }
}