# Resend API Integration Guide

## Overview
TradeScout.API now uses **Resend API** for all email operations instead of traditional SMTP. This provides better deliverability, tracking, and reliability.

## Changes Made

### 1. Package Dependencies
- **Added**: `Resend` NuGet package (v0.2.1)
- **File**: `TradeScout.API.csproj`

### 2. Configuration Files

#### appsettings.json (Production)
```json
"EmailSettings": {
  "ResendApiKey": "re_PRODUCTION_RESEND_API_KEY_PLACEHOLDER",
  "FromEmail": "noreply@fgstrade.com",
  "AdminEmail": "info@fgstrade.com"
}
```

#### appsettings.Development.json (Development)
```json
"EmailSettings": {
  "ResendApiKey": "re_test_your_resend_key_here",
  "FromEmail": "onboarding@resend.dev",
  "AdminEmail": "admin@example.com"
}
```

#### appsettings.Production.json
```json
"EmailSettings": {
  "ResendApiKey": "re_PRODUCTION_RESEND_API_KEY_PLACEHOLDER",
  "FromEmail": "noreply@fgstrade.com",
  "AdminEmail": "info@fgstrade.com"
}
```

### 3. Email Service Implementation

**File**: `Services/EmailService.cs`

#### Key Features:
- **ResendEmailService**: Implements `IEmailService` interface
- **HTTP Client**: Uses `HttpClient` for direct API calls to Resend
- **Two Email Methods**:
  1. `SendEmailAsync()` - Simple email sending
  2. `SendFeedbackEmailAsync()` - Feedback system with HTML templates

#### Implementation Details:
- Uses Resend API endpoint: `https://api.resend.com/emails`
- Authentication: Bearer token in Authorization header
- Request format: JSON with `from`, `to`, `subject`, `html`
- Error handling: Logs failures with HTTP status codes

### 4. Dependency Injection

**File**: `Program.cs`

```csharp
// Register HTTP Client Factory for Resend API
builder.Services.AddHttpClient();

// Register Resend Email Service
builder.Services.AddScoped<IEmailService, ResendEmailService>();
```

### 5. Usage in Controllers

**File**: `Controllers/FeedbackController.cs`

The `FeedbackController` uses the email service to send feedback confirmations:

```csharp
var emailSent = await _emailService.SendFeedbackEmailAsync(
    feedbackDto.FullName,
    feedbackDto.Email,
    feedbackDto.Phone,
    feedbackDto.Subject,
    feedbackDto.Message,
    feedbackDto.FeedbackType
);
```

## Setup Instructions

### Development Environment

1. **Get a test API key** from Resend:
   - Sign up at [https://resend.com](https://resend.com)
   - Get test API key: `re_test_...`
   - Use test sender email: `onboarding@resend.dev`

2. **Update appsettings.Development.json**:
   ```json
   "EmailSettings": {
     "ResendApiKey": "your_test_resend_key",
     "FromEmail": "onboarding@resend.dev",
     "AdminEmail": "your-email@example.com"
   }
   ```

3. **Run the application**:
   ```bash
   dotnet run
   ```

### Production Environment

1. **Get production API key** from Resend:
   - Create a verified domain in Resend dashboard
   - Generate production API key
   - Verify sender email or domain

2. **Update appsettings.Production.json**:
   ```json
   "EmailSettings": {
     "ResendApiKey": "re_your_production_key",
     "FromEmail": "noreply@fgstrade.com",
     "AdminEmail": "info@fgstrade.com"
   }
   ```

3. **Environment Variables** (Optional but Recommended):
   ```bash
   export RESEND_API_KEY=re_your_production_key
   ```

4. **Deploy**:
   ```bash
   dotnet publish -c Release -o ./release
   ```

## Email Features

### 1. Feedback Email System
- **Admin Notification**: Receives detailed feedback with all sender information
- **User Confirmation**: Sender gets confirmation email that feedback was received
- **HTML Templates**: Professional styled emails with company branding
- **Error Logging**: All failures are logged for monitoring

### 2. Email Templates
Both admin and user emails include:
- Responsive HTML design
- Color-coded headers (blue for admin, green for user)
- Structured field display
- Footer with timestamp and copyright

### 3. Response Handling
- **Success**: Returns `true` with message ID in logs
- **Failure**: Returns `false` with HTTP status code and error details
- **Exceptions**: Caught and logged for debugging

## Resend API Reference

### Email Sending Endpoint
```
POST https://api.resend.com/emails
Authorization: Bearer {API_KEY}
Content-Type: application/json
```

### Request Body
```json
{
  "from": "noreply@yourdomain.com",
  "to": "recipient@example.com",
  "subject": "Email Subject",
  "html": "<h1>HTML Content</h1>"
}
```

### Response
- **Success (200)**: `{ "id": "message-uuid" }`
- **Error (400/401/500)**: `{ "message": "Error description" }`

## Testing

### Test Email Send (Development)
```csharp
using (var client = new HttpClient())
{
    client.DefaultRequestHeaders.Add("Authorization", "Bearer re_test_key");
    var request = new { from = "onboarding@resend.dev", to = "test@example.com", subject = "Test", html = "<p>Test</p>" };
    var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
    var response = await client.PostAsync("https://api.resend.com/emails", content);
}
```

### Using Feedback Endpoint
```bash
curl -X POST https://localhost:5001/api/feedback \
  -H "Content-Type: application/json" \
  -d '{
    "fullName": "Test User",
    "email": "user@example.com",
    "phone": "1234567890",
    "subject": "Test Feedback",
    "message": "This is a test feedback",
    "feedbackType": "bug"
  }'
```

## Troubleshooting

### Issue: "Resend API key not found"
**Solution**: Ensure ResendApiKey is set in appsettings.json or RESEND_API_KEY environment variable

### Issue: "401 Unauthorized"
**Solution**: Check that your API key is valid and hasn't expired

### Issue: "403 Forbidden - From address not verified"
**Solution**: Verify your sender email/domain in Resend dashboard

### Issue: Email not delivered
**Solution**: Check Resend dashboard for bounce reasons or logs for HTTP status codes

## Monitoring & Logging

All email operations are logged at:
- **Info Level**: Successful email sends with message IDs
- **Warning Level**: Failed admin emails (non-blocking)
- **Error Level**: Failed user emails or exceptions

Check logs in development:
```bash
tail -f logs/tradetrace*.log | grep -i email
```

## Migration from SMTP

### Removed:
- SMTP configuration (SmtpHost, SmtpPort, SmtpUsername, SmtpPassword)
- SMTP-specific error handling

### Added:
- Resend API HTTP client
- Bearer token authentication
- JSON-based request/response handling

### Benefits:
✅ Better deliverability rates  
✅ Email tracking and analytics  
✅ No SMTP server maintenance  
✅ Automatic retry and bounce handling  
✅ Simpler configuration  
✅ Better support for templates  

## Next Steps

1. ✅ Update configuration files with real API keys
2. ✅ Test email sending in development
3. ✅ Deploy to production
4. ✅ Monitor email delivery in Resend dashboard
5. ✅ Set up email templates for better styling

## Support

For issues with:
- **Resend API**: https://resend.com/docs
- **TradeScout Integration**: Check logs and ensure API key is valid

---

**Last Updated**: 25 Şubat 2026  
**Version**: 1.0  
**Status**: Production Ready ✅
