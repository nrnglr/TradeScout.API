using Microsoft.EntityFrameworkCore;
using TradeScout.API.Data;
using TradeScout.API.Models;

namespace TradeScout.API.Services;

/// <summary>
/// Eğitim (TRG-) ödemesi sonuçlanınca: kaydı günceller ve (sadece başarılıysa, sadece bir kez) mailleri atar.
/// Hem PaymentController hem ParatikaPaymentController callback'i bunu kullanır.
/// </summary>
public static class TrainingPaymentHandler
{
    public static async Task CompleteAsync(
        ApplicationDbContext db, IEmailService email, ILogger logger,
        string registrationCode, bool success)
    {
        var reg = await db.TrainingRegistrations
            .FirstOrDefaultAsync(r => r.RegistrationCode == registrationCode);

        if (reg == null)
        {
            logger.LogWarning("⚠️ Eğitim kaydı bulunamadı | {Code}", registrationCode);
            return;
        }

        // Aynı callback ikinci kez gelirse tekrar işlem/mail yapma
        if (reg.PaymentStatus == "Completed") return;

        reg.PaymentStatus = success ? "Completed" : "Failed";
        reg.PaidAt = success ? DateTime.UtcNow : null;
        await db.SaveChangesAsync();

        logger.LogInformation("🎓 Eğitim kaydı güncellendi | {Code} → {Status}", reg.RegistrationCode, reg.PaymentStatus);

        if (success)
            await SendEmailsAsync(email, logger, reg);
    }

    private static async Task SendEmailsAsync(IEmailService email, ILogger logger, TrainingRegistration reg)
    {
        static string e(string? text) => System.Net.WebUtility.HtmlEncode(text ?? "");

        try
        {
            var adminBody = $@"
                <h3>Yeni Eğitim Kaydı (Ödeme Alındı)</h3>
                <p><b>Kayıt Kodu:</b> {e(reg.RegistrationCode)}<br>
                <b>Ad Soyad:</b> {e(reg.FirstName)} {e(reg.LastName)}<br>
                <b>E-posta:</b> {e(reg.Email)}<br>
                <b>Telefon:</b> {e(reg.Phone)}<br>
                <b>Şehir:</b> {e(reg.City)}<br>
                <b>Meslek/Firma:</b> {e(reg.Occupation ?? "-")}<br>
                <b>Dönem:</b> {e(reg.SelectedSlot)}<br>
                <b>Tutar:</b> {reg.Amount:N2} TL<br>
                <b>Ödeme Tarihi (UTC):</b> {reg.PaidAt:dd.MM.yyyy HH:mm}</p>";

            var adminSent = await email.SendEmailAsync(
                "info@fgstrade.com",
                $"[FGS Trade] Yeni eğitim kaydı: {reg.FirstName} {reg.LastName}",
                adminBody);
            if (!adminSent)
                logger.LogWarning("Eğitim admin maili gönderilemedi | {Code}", reg.RegistrationCode);

            var userBody = $@"
                <h3>Ödemeniz alındı, kaydınız tamamlandı ✅</h3>
                <p>Merhaba {e(reg.FirstName)},</p>
                <p>Dış Ticaret &amp; E-Ticaret Sertifika Programı için ödemeniz başarıyla alındı.</p>
                <p><b>Kayıt kodunuz:</b> {e(reg.RegistrationCode)}<br>
                <b>Seçtiğiniz dönem:</b> {e(reg.SelectedSlot)}</p>
                <p>Sorularınız için bu e-postayı yanıtlayabilirsiniz.</p>
                <p><b>FGS TRADE Ekibi</b></p>";

            var userSent = await email.SendEmailAsync(
                reg.Email, "Eğitim kaydınız tamamlandı - FGS Trade", userBody);
            if (!userSent)
                logger.LogWarning("Eğitim müşteri maili gönderilemedi | {Code}", reg.RegistrationCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Eğitim mailleri gönderilirken hata | {Code}", reg.RegistrationCode);
        }
    }
}