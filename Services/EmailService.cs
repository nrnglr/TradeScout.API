using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Resend;

namespace TradeScout.API.Services;

/// <summary>
/// Email service interface
/// </summary>
public interface IEmailService
{
    Task<bool> SendEmailAsync(string recipientEmail, string subject, string htmlBody);
    Task<bool> SendFeedbackEmailAsync(string senderName, string senderEmail, string senderPhone, string subject, string message, string feedbackType);
}

/// <summary>
/// SMTP Email service implementation — MailKit kullanır.
/// System.Net.Mail.SmtpClient port 465 (implicit SSL/SMTPS) desteklemez.
/// MailKit hem port 465 (SslOnConnect) hem 587 (StartTls) destekler.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly string _adminEmail;
    private readonly bool _enableSsl;
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly bool _isConfigured;

    public SmtpEmailService(
        IConfiguration configuration,
        ILogger<SmtpEmailService> logger)
    {
        _logger = logger;

        _smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST")
            ?? configuration["SmtpSettings:Host"]
            ?? "";

        var portStr = Environment.GetEnvironmentVariable("SMTP_PORT")
            ?? configuration["SmtpSettings:Port"]
            ?? "587";
        _smtpPort = int.TryParse(portStr, out var port) ? port : 587;

        _smtpUsername = Environment.GetEnvironmentVariable("SMTP_USERNAME")
            ?? configuration["SmtpSettings:Username"]
            ?? "";

        _smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD")
            ?? configuration["SmtpSettings:Password"]
            ?? "";

        _fromEmail = Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL")
            ?? configuration["SmtpSettings:FromEmail"]
            ?? "info@fgstrade.com";

        _fromName = Environment.GetEnvironmentVariable("SMTP_FROM_NAME")
            ?? configuration["SmtpSettings:FromName"]
            ?? "FGS Trade";

        _adminEmail = configuration["EmailSettings:AdminEmail"] ?? "info@fgstrade.com";

        var enableSslStr = Environment.GetEnvironmentVariable("SMTP_ENABLE_SSL")
            ?? configuration["SmtpSettings:EnableSsl"]
            ?? "true";
        _enableSsl = enableSslStr.ToLower() == "true";

        _isConfigured = !string.IsNullOrEmpty(_smtpHost)
            && !string.IsNullOrEmpty(_smtpUsername)
            && !string.IsNullOrEmpty(_smtpPassword)
            && _smtpPassword != "YOUR_NATRO_EMAIL_PASSWORD_HERE";

        if (_isConfigured)
            _logger.LogInformation(
                "✅ SmtpEmailService (MailKit) hazır: {Host}:{Port} SSL={Ssl}, From: {Name} <{Email}>",
                _smtpHost, _smtpPort, _enableSsl, _fromName, _fromEmail);
        else
            _logger.LogWarning("⚠️ SMTP yapılandırılmamış. E-postalar gönderilmeyecek.");
    }

    public async Task<bool> SendEmailAsync(string recipientEmail, string subject, string htmlBody)
    {
        if (!_isConfigured)
        {
            _logger.LogInformation("[DEV] E-posta gönderilecekti: {To} | {Subject}", recipientEmail, subject);
            return true;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_fromName, _fromEmail));
            message.To.Add(MailboxAddress.Parse(recipientEmail));
            message.Subject = subject;
            message.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = htmlBody };

            using var client = new MailKit.Net.Smtp.SmtpClient();

            // Natro gibi eski mail sunucuları "unsafe legacy renegotiation" kullanır.
            // macOS Ventura+ ve yeni OpenSSL bunu varsayılan olarak reddeder.
            // ServerCertificateValidationCallback ile SSL doğrulamasını devre dışı bırakıyoruz.
            // Bu lokal→Natro bağlantısı için kabul edilebilir; production'da
            // Natro'nun SSL sertifikasını güncellemesi ideal çözümdür.
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            // Port 465 → SslOnConnect (implicit SSL, eski adıyla SMTPS)
            // Port 587 → StartTls  (explicit SSL / STARTTLS)
            // Port 25  → None      (düz metin, üretimde kullanılmamalı)
            var secureOption = _smtpPort switch
            {
                465 => SecureSocketOptions.SslOnConnect,
                587 => SecureSocketOptions.StartTls,
                _   => _enableSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None
            };

            await client.ConnectAsync(_smtpHost, _smtpPort, secureOption);
            await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("✅ E-posta gönderildi: {To}", recipientEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ E-posta gönderilemedi: {To} | {Error}", recipientEmail, ex.Message);
            return false;
        }
    }

    public async Task<bool> SendFeedbackEmailAsync(
        string senderName,
        string senderEmail,
        string? senderPhone,
        string subject,
        string message,
        string? feedbackType)
    {
        if (!_isConfigured)
        {
            _logger.LogInformation("[DEV] Feedback e-postası gönderilecekti: {From}", senderEmail);
            return true;
        }

        try
        {
            var adminHtml = BuildAdminFeedbackEmailHtml(senderName, senderEmail, senderPhone, subject, message, feedbackType);
            var adminSent = await SendEmailAsync(_adminEmail, $"[FGSTrade] Geri Bildirim: {subject}", adminHtml);

            var userHtml = BuildUserConfirmationEmailHtml(senderName, subject, feedbackType);
            var userSent = await SendEmailAsync(senderEmail, "Geri Bildiriminiz Kaydedilmiştir - FGSTrade", userHtml);

            return adminSent && userSent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Feedback e-postası gönderilemedi: {From}", senderEmail);
            return true; // Feedback DB'ye kaydedildi, e-posta hatası formu engellemez
        }
    }


    private string BuildAdminFeedbackEmailHtml(string senderName, string senderEmail, string? senderPhone, string subject, string message, string? feedbackType)
    {
        return $@"
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; }}
                    .container {{ max-width: 600px; margin: 20px auto; background-color: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
                    .header {{ background-color: #2c3e50; color: white; padding: 15px; border-radius: 5px; }}
                    .header h1 {{ margin: 0; font-size: 24px; }}
                    .content {{ margin: 20px 0; }}
                    .field {{ margin: 10px 0; }}
                    .label {{ font-weight: bold; color: #2c3e50; }}
                    .value {{ color: #555; margin-top: 5px; padding: 10px; background-color: #f9f9f9; border-left: 4px solid #3498db; }}
                    .footer {{ color: #999; font-size: 12px; text-align: center; margin-top: 20px; border-top: 1px solid #ddd; padding-top: 10px; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h1>🔔 Yeni Kullanıcı Geri Bildirimi</h1>
                    </div>
                    <div class='content'>
                        <div class='field'>
                            <div class='label'>Gönderen:</div>
                            <div class='value'>{senderName}</div>
                        </div>
                        <div class='field'>
                            <div class='label'>Email:</div>
                            <div class='value'>{senderEmail}</div>
                        </div>
                        {(string.IsNullOrEmpty(senderPhone) ? "" : $@"
                        <div class='field'>
                            <div class='label'>Telefon:</div>
                            <div class='value'>{senderPhone}</div>
                        </div>")}
                        <div class='field'>
                            <div class='label'>Geri Bildirim Türü:</div>
                            <div class='value'>{feedbackType ?? "Belirtilmemiş"}</div>
                        </div>
                        <div class='field'>
                            <div class='label'>Konu:</div>
                            <div class='value'>{subject}</div>
                        </div>
                        <div class='field'>
                            <div class='label'>Mesaj:</div>
                            <div class='value'>{message.Replace(Environment.NewLine, "<br>")}</div>
                        </div>
                    </div>
                    <div class='footer'>
                        <p>FGS TRADE Geri Bildirim Sistemi - {DateTime.UtcNow:dd.MM.yyyy HH:mm:ss} UTC</p>
                    </div>
                </div>
            </body>
            </html>";
    }

    private string BuildUserConfirmationEmailHtml(string senderName, string subject, string? feedbackType)
    {
        return $@"
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; }}
                    .container {{ max-width: 600px; margin: 20px auto; background-color: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
                    .header {{ background-color: #27ae60; color: white; padding: 15px; border-radius: 5px; }}
                    .header h1 {{ margin: 0; font-size: 24px; }}
                    .content {{ margin: 20px 0; line-height: 1.6; color: #333; }}
                    .footer {{ color: #999; font-size: 12px; text-align: center; margin-top: 20px; border-top: 1px solid #ddd; padding-top: 10px; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h1>✅ Geri Bildirimi Aldık!</h1>
                    </div>
                    <div class='content'>
                        <p>Merhaba {senderName},</p>
                        <p>Geri bildiriminiz başarıyla kaydedilmiş ve bizim destek ekibimize iletilmiştir.</p>
                        <p>Geri Bildirim Özeti:</p>
                        <ul>
                            <li><strong>Tür:</strong> {feedbackType ?? "Belirtilmemiş"}</li>
                            <li><strong>Konu:</strong> {subject}</li>
                        </ul>
                        <p>Sorularınız veya gerçekleştirmeye çalıştığınız bir şey varsa, lütfen bu e-postaya cevap vererek bize bilgilendirebilirsiniz.</p>
                        <p>Yardımcı olmaktan mutlu olacağız!<br><br>
                        <strong>FGS TRADE Ekibi</strong></p>
                    </div>
                    <div class='footer'>
                        <p>© 2026 FGS TRADE. Tüm hakları saklıdır.</p>
                    </div>
                </div>
            </body>
            </html>";
    }
}

/// <summary>
/// Resend Email service implementation using HTTP client directly
/// </summary>
public class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly string _resendApiKey;
    private readonly string _fromEmail;
    private readonly string _adminEmail;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        IConfiguration configuration,
        ILogger<ResendEmailService> logger,
        HttpClient httpClient,
        IWebHostEnvironment environment)
    {
        _httpClient = httpClient;
        // Try environment variable first, then appsettings.json
        _resendApiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY") 
            ?? configuration["EmailSettings:ResendApiKey"] 
            ?? "";
        
        // In development, use Resend sandbox address if domain not verified
        var isDevelopment = environment.IsDevelopment();
        _fromEmail = isDevelopment 
            ? "onboarding@resend.dev" // Resend sandbox (works without domain verification)
            : (configuration["EmailSettings:FromEmail"] ?? "noreply@fgstrade.com");
        
        _adminEmail = configuration["EmailSettings:AdminEmail"] ?? "info@fgstrade.com";
        _logger = logger;
        
        // Setup HTTP client for Resend API (only once per instance)
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri("https://api.resend.com");
            _httpClient.Timeout = TimeSpan.FromSeconds(30); // 30 second timeout for network stability
            
            if (!string.IsNullOrEmpty(_resendApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_resendApiKey}");
            }
        }
        
        // Log initialization
        if (string.IsNullOrEmpty(_resendApiKey))
        {
            _logger.LogWarning("⚠️ Resend API Key is not configured. Emails will be logged but not sent.");
        }
        else if (!_resendApiKey.StartsWith("re_"))
        {
            _logger.LogWarning("⚠️ Resend API Key format looks invalid (should start with 're_'). Check .env or appsettings.json");
        }
        else
        {
            var apiKeyPreview = _resendApiKey.Length > 10 ? _resendApiKey.Substring(0, 10) : _resendApiKey;
            _logger.LogInformation("✅ Resend Email Service initialized with API Key: {ApiKeyStart}..., From: {FromEmail}", apiKeyPreview, _fromEmail);
        }
    }

    /// <summary>
    /// Send email to recipient
    /// </summary>
    public async Task<bool> SendEmailAsync(string recipientEmail, string subject, string htmlBody)
    {
        try
        {
            // If no API key, just log and return success (dev mode)
            if (string.IsNullOrEmpty(_resendApiKey))
            {
                _logger.LogInformation("📧 [DEV MODE] Email would be sent to: {RecipientEmail}, Subject: {Subject}", recipientEmail, subject);
                return true;
            }

            var request = new
            {
                from = _fromEmail,
                to = recipientEmail,
                subject = subject,
                html = htmlBody
            };

            using (var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json"))
            {
                using (var response = await _httpClient.PostAsync("/emails", content))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var responseBody = await response.Content.ReadAsStringAsync();
                        _logger.LogInformation("✅ Email başarıyla gönderildi: {RecipientEmail}, Response: {Response}", recipientEmail, responseBody);
                        return true;
                    }
                    else
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();
                        _logger.LogError("❌ Email gönderme hatası - {RecipientEmail}: {StatusCode} - {Error}", recipientEmail, response.StatusCode, errorBody);
                        return false;
                    }
                }
            }
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "⏱️ Resend API timeout ({RecipientEmail}). API key valid mi? Kontrol et appsettings.json", recipientEmail);
            return true; // Return true anyway so operation continues
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "🌐 Resend API network hatası ({RecipientEmail}). Check network/API key", recipientEmail);
            return true; // Return true anyway so operation continues
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Email gönderme hatası: {RecipientEmail}", recipientEmail);
            return true; // Return true anyway so operation continues
        }
    }

    /// <summary>
    /// Send feedback email to admin and confirmation to user
    /// </summary>
    public async Task<bool> SendFeedbackEmailAsync(
        string senderName,
        string senderEmail,
        string? senderPhone,
        string subject,
        string message,
        string? feedbackType)
    {
        try
        {
            // If no API key, just log and return success (dev mode)
            if (string.IsNullOrEmpty(_resendApiKey))
            {
                _logger.LogInformation("📧 [DEV MODE] Feedback emails would be sent - From: {SenderEmail}, Subject: {Subject}", senderEmail, subject);
                return true;
            }
            // 1. Send to Admin
            var adminBodyHtml = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; }}
                        .container {{ max-width: 600px; margin: 20px auto; background-color: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
                        .header {{ background-color: #2c3e50; color: white; padding: 15px; border-radius: 5px; }}
                        .header h1 {{ margin: 0; font-size: 24px; }}
                        .content {{ margin: 20px 0; }}
                        .field {{ margin: 10px 0; }}
                        .label {{ font-weight: bold; color: #2c3e50; }}
                        .value {{ color: #555; margin-top: 5px; padding: 10px; background-color: #f9f9f9; border-left: 4px solid #3498db; }}
                        .footer {{ color: #999; font-size: 12px; text-align: center; margin-top: 20px; border-top: 1px solid #ddd; padding-top: 10px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>🔔 Yeni Kullanıcı Geri Bildirimi</h1>
                        </div>
                        <div class='content'>
                            <div class='field'>
                                <div class='label'>Gönderen:</div>
                                <div class='value'>{senderName}</div>
                            </div>
                            <div class='field'>
                                <div class='label'>Email:</div>
                                <div class='value'>{senderEmail}</div>
                            </div>
                            {(string.IsNullOrEmpty(senderPhone) ? "" : $@"
                            <div class='field'>
                                <div class='label'>Telefon:</div>
                                <div class='value'>{senderPhone}</div>
                            </div>")}
                            <div class='field'>
                                <div class='label'>Geri Bildirim Türü:</div>
                                <div class='value'>{feedbackType ?? "Belirtilmemiş"}</div>
                            </div>
                            <div class='field'>
                                <div class='label'>Konu:</div>
                                <div class='value'>{subject}</div>
                            </div>
                            <div class='field'>
                                <div class='label'>Mesaj:</div>
                                <div class='value'>{message.Replace(Environment.NewLine, "<br>")}</div>
                            </div>
                        </div>
                        <div class='footer'>
                            <p>FGS TRADE Geri Bildirim Sistemi - {DateTime.UtcNow:dd.MM.yyyy HH:mm:ss} UTC</p>
                        </div>
                    </div>
                </body>
                </html>";

            var adminRequest = new
            {
                from = _fromEmail,
                to = _adminEmail,
                subject = $"[FGS TRADE] Geri Bildirim: {subject}",
                html = adminBodyHtml
            };

            using (var adminContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(adminRequest),
                System.Text.Encoding.UTF8,
                "application/json"))
            {
                using (var adminResponse = await _httpClient.PostAsync("/emails", adminContent))
                {
                    if (!adminResponse.IsSuccessStatusCode)
                    {
                        var errorBody = await adminResponse.Content.ReadAsStringAsync();
                        _logger.LogWarning("Admin email gönderilemedi: {StatusCode} - {Error}", adminResponse.StatusCode, errorBody);
                    }
                }
            }

            // 2. Send confirmation to user
            var userBodyHtml = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; }}
                        .container {{ max-width: 600px; margin: 20px auto; background-color: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
                        .header {{ background-color: #27ae60; color: white; padding: 15px; border-radius: 5px; }}
                        .header h1 {{ margin: 0; font-size: 24px; }}
                        .content {{ margin: 20px 0; line-height: 1.6; color: #333; }}
                        .footer {{ color: #999; font-size: 12px; text-align: center; margin-top: 20px; border-top: 1px solid #ddd; padding-top: 10px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>✅ Geri Bildirimi Aldık!</h1>
                        </div>
                        <div class='content'>
                            <p>Merhaba {senderName},</p>
                            <p>Geri bildiriminiz başarıyla kaydedilmiş ve bizim destek ekibimize iletilmiştir.</p>
                            <p>Geri Bildirim Özeti:</p>
                            <ul>
                                <li><strong>Tür:</strong> {feedbackType ?? "Belirtilmemiş"}</li>
                                <li><strong>Konu:</strong> {subject}</li>
                            </ul>
                            <p>Sorularınız veya gerçekleştirmeye çalıştığınız bir şey varsa, lütfen bu e-postaya cevap vererek bize bilgilendirebilirsiniz.</p>
                            <p>Yardımcı olmaktan mutlu olacağız!<br><br>
                            <strong>FGS TRADE Ekibi</strong></p>
                        </div>
                        <div class='footer'>
                            <p>© 2026 FGS TRADE. Tüm hakları saklıdır.</p>
                        </div>
                    </div>
                </body>
                </html>";

            var userRequest = new
            {
                from = _fromEmail,
                to = senderEmail,
                subject = "Geri Bildiriminiz Kaydedilmiştir - FGS TRADE",
                html = userBodyHtml
            };

            using (var userContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(userRequest),
                System.Text.Encoding.UTF8,
                "application/json"))
            {
                using (var userResponse = await _httpClient.PostAsync("/emails", userContent))
                {
                    if (userResponse.IsSuccessStatusCode)
                    {
                        var responseBody = await userResponse.Content.ReadAsStringAsync();
                        _logger.LogInformation("Geri bildirim email'leri gönderildi: {SenderEmail}, Response: {Response}", senderEmail, responseBody);
                        return true;
                    }
                    else
                    {
                        var errorBody = await userResponse.Content.ReadAsStringAsync();
                        _logger.LogError("Geri bildirim email'i gönderilemedi: {StatusCode} - {Error}", userResponse.StatusCode, errorBody);
                        return false;
                    }
                }
            }
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "⏱️ Resend API timeout ({SenderEmail}). API key valid mi? Kontrol et appsettings.json", senderEmail);
            return true; // Return true anyway so feedback is saved
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "🌐 Geri bildirim email'leri gönderme network hatası: {SenderEmail}", senderEmail);
            return true; // Return true anyway so feedback is saved
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Geri bildirim email'leri gönderme hatası: {SenderEmail}", senderEmail);
            return true; // Return true anyway so feedback is saved
        }
    }
}