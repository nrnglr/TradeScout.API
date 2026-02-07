# TradeScout API - Quick Reference

## Base URL
```
http://localhost:5000
```

## Available Endpoints

### 🏥 Health Check
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/` | No | Check API health status |

### 🔐 Authentication
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/auth/register` | No | Register new user |
| POST | `/api/auth/login` | No | Login existing user |

### 🔍 Scraper
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/scraper/scrape` | Yes | Start scraping businesses |
| GET | `/api/scraper/download/{jobId}` | Yes | Download results as Excel |
| GET | `/api/scraper/history` | Yes | Get scraping job history |
| GET | `/api/scraper/credits` | Yes | Get current credit balance |

## Quick Start Examples

### 1. Health Check
```bash
curl http://localhost:5000/
```

### 2. Register User
```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "SecurePass123!",
    "fullName": "John Doe"
  }'
```

### 3. Login
```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "SecurePass123!"
  }'
```

### 4. Start Scraping (with token)
```bash
curl -X POST http://localhost:5000/api/scraper/scrape \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -d '{
    "searchQuery": "restaurants in Istanbul",
    "maxResults": 50
  }'
```

### 5. Check Credits
```bash
curl http://localhost:5000/api/auth/credits \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### 6. Download Excel
```bash
curl http://localhost:5000/api/scraper/download/1 \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -o results.xlsx
```

## Response Status Codes

| Code | Meaning |
|------|---------|
| 200 | Success |
| 201 | Created successfully |
| 400 | Bad request (validation error) |
| 401 | Unauthorized (missing/invalid token) |
| 404 | Resource not found |
| 409 | Conflict (e.g., email already exists) |
| 500 | Internal server error |

## Authentication

Most endpoints require JWT authentication. Include the token in the Authorization header:

```
Authorization: Bearer <your_jwt_token>
```

You receive a JWT token after successful registration or login.

## CORS Configuration

The API allows requests from:
- `http://localhost:3000` (React/Next.js default)
- `http://localhost:5173` (Vite default)
- `http://localhost:4200` (Angular default)
- `http://localhost:3001` (Alternative port)

## Credits System

- **Free Tier**: 5 credits on registration
- **1 Credit** = Ability to scrape **1 business**
- Credits are deducted based on the `maxResults` parameter
- Example: Scraping 50 businesses costs 50 credits

## Rate Limiting & Ban Protection

The scraper includes:
- Random delays (2-5 seconds between actions)
- Human-like scrolling behavior
- User-agent rotation
- Request rate limiting
- Duplicate business filtering

## Support

For detailed documentation, see:
- `README.md` - General project overview
- `SCRAPER_README.md` - Scraper implementation details
- `FRONTEND_KULLANIM.md` - Frontend integration guide
- `HEALTH_CHECK.md` - Health check endpoint details
- `KURULUM.md` - Installation instructions (Turkish)

## Running the API

```bash
cd /Users/nuranguler/Desktop/TradeScout/FGS_APİ/TradeScout.API
dotnet run
```

The API will start on: `http://localhost:5000`
