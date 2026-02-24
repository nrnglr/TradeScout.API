using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Hosting;

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
/// SMTP Email service implementation
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly IWebHostEnvironment _environment;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Send email to recipient
    /// </summary>
    public async Task<bool> SendEmailAsync(string recipientEmail, string subject, string htmlBody)
    {
        try
        {
            var smtpHost = _configuration["EmailSettings:SmtpHost"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            var smtpUsername = _configuration["EmailSettings:SmtpUsername"] ?? throw new InvalidOperationException("Email username not configured");
            var smtpPassword = _configuration["EmailSettings:SmtpPassword"] ?? throw new InvalidOperationException("Email password not configured");
            var fromEmail = _configuration["EmailSettings:FromEmail"] ?? smtpUsername;
            var fromName = _configuration["EmailSettings:FromName"] ?? "FGSTrade";

            using (var smtpClient = new SmtpClient(smtpHost, smtpPort))
            {
                smtpClient.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                smtpClient.EnableSsl = true;
                smtpClient.Timeout = 10000;
                
                // Development ortamında SSL sertifika doğrulamasını devre dışı bırak
                // Production'da normal doğrulama yapılacak
                if (_environment.IsDevelopment())
                {
                    System.Net.ServicePointManager.ServerCertificateValidationCallback = 
                        (sender, certificate, chain, sslPolicyErrors) => true;
                }

                using (var mailMessage = new MailMessage(new MailAddress(fromEmail, fromName), new MailAddress(recipientEmail)))
                {
                    mailMessage.Subject = subject;
                    mailMessage.Body = htmlBody;
                    mailMessage.IsBodyHtml = true;

                    await smtpClient.SendMailAsync(mailMessage);
                }
            }

            _logger.LogInformation("Email başarıyla gönderildi: {RecipientEmail}", recipientEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email gönderme hatası - Host: {Host}:{Port}, Username: {Username}, Error: {Message}", 
                _configuration["EmailSettings:SmtpHost"],
                _configuration["EmailSettings:SmtpPort"],
                _configuration["EmailSettings:SmtpUsername"],
                ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Send feedback email to admin and confirmation to user
    /// </summary>
    public async Task<bool> SendFeedbackEmailAsync(string senderName, string senderEmail, string? senderPhone, string subject, string message, string? feedbackType)
    {
        try
        {
            var adminEmail = _configuration["EmailSettings:AdminEmail"] ?? "info@fgstrade.com";

            // 1. Send to Admin
            var adminBodyHtml = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; }}
                        .container {{ max-width: 600px; margin: 20px auto; background-color: white; padding: 20px; border-radius: 8px; }}
                        .header {{ background-color: #2c3e50; color: white; padding: 15px; border-radius: 5px; }}
                        .content {{ margin: 20px 0; }}
                        .field {{ margin: 15px 0; }}
                        .label {{ font-weight: bold; color: #2c3e50; }}
                        .footer {{ margin-top: 20px; padding-top: 15px; border-top: 1px solid #ddd; color: #888; font-size: 12px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>Yeni Feedback Aldınız</h2>
                        </div>
                        <div class='content'>
                            <div class='field'>
                                <span class='label'>Gönderen:</span> {senderName}
                            </div>
                            <div class='field'>
                                <span class='label'>Email:</span> <a href='mailto:{senderEmail}'>{senderEmail}</a>
                            </div>
                            {(string.IsNullOrEmpty(senderPhone) ? "" : $"<div class='field'><span class='label'>Telefon:</span> {senderPhone}</div>")}
                            <div class='field'>
                                <span class='label'>Feedback Türü:</span> {feedbackType ?? "Belirtilmedi"}
                            </div>
                            <div class='field'>
                                <span class='label'>Başlık:</span> {subject}
                            </div>
                            <div class='field'>
                                <span class='label'>Mesaj:</span>
                                <p>{message.Replace("\n", "<br>")}</p>
                            </div>
                        </div>
                        <div class='footer'>
                            <p>Bu email FGSTrade feedback sistemi tarafından otomatik olarak gönderilmiştir.</p>
                        </div>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(adminEmail, $"[FGSTrade Feedback] {subject}", adminBodyHtml);

            // 2. Send confirmation to user
            var userBodyHtml = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; }}
                        .container {{ max-width: 600px; margin: 20px auto; background-color: white; padding: 20px; border-radius: 8px; }}
                        .header {{ background-color: #27ae60; color: white; padding: 15px; border-radius: 5px; }}
                        .content {{ margin: 20px 0; }}
                        .footer {{ margin-top: 20px; padding-top: 15px; border-top: 1px solid #ddd; color: #888; font-size: 12px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>Feedback'iniz Alındı</h2>
                        </div>
                        <div class='content'>
                            <p>Merhaba {senderName},</p>
                            <p>Geri bildiriminiz başarıyla alınmıştır. En kısa zamanda incelenerek size dönüş yapılacaktır.</p>
                            <p><strong>Başlık:</strong> {subject}</p>
                            <p>Teşekkür ederiz!</p>
                        </div>
                        <div class='footer'>
                            <p>FGSTrade Destek Ekibi</p>
                        </div>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(senderEmail, "Feedback'iniz Alındı - FGSTrade", userBodyHtml);

            _logger.LogInformation("Feedback emaili başarıyla gönderildi: {AdminEmail}, {SenderEmail}", adminEmail, senderEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Feedback email gönderme hatası");
            return false;
        }
    }
}
