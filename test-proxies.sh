#!/bin/bash

# TradeScout Proxy Test Script
# Bu script proxy'leri test eder ve detaylı sonuç verir

echo "🧪 TradeScout Proxy Test Suite"
echo "================================"
echo ""

# Configuration
API_URL="http://localhost:5000"
EMAIL="test@example.com"
PASSWORD="Test123!"

echo "1️⃣  API Sağlık Kontrolü..."
HEALTH=$(curl -s $API_URL)
echo "$HEALTH" | jq '.'
echo ""

echo "2️⃣  Kullanıcı Girişi..."
LOGIN_RESPONSE=$(curl -s -X POST "$API_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}")

TOKEN=$(echo $LOGIN_RESPONSE | jq -r '.token')

if [ "$TOKEN" == "null" ] || [ -z "$TOKEN" ]; then
    echo "❌ Giriş başarısız! Lütfen kullanıcı oluşturun:"
    echo "curl -X POST $API_URL/api/auth/register -H 'Content-Type: application/json' -d '{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\",\"fullName\":\"Test User\"}'"
    exit 1
fi

echo "✅ Giriş başarılı! Token alındı."
echo ""

echo "3️⃣  Proxy Durumu Kontrolü..."
curl -s -X GET "$API_URL/api/proxy/status" \
  -H "Authorization: Bearer $TOKEN" | jq '.'
echo ""

echo "4️⃣  Tüm Proxy'leri Test Et..."
echo "⏳ Bu işlem biraz zaman alabilir..."
echo ""

curl -s -X POST "$API_URL/api/proxy/test-all" \
  -H "Authorization: Bearer $TOKEN" | jq '.'
echo ""

echo "✅ Test tamamlandı!"
echo ""
echo "💡 Çalışan proxy'leri görmek için:"
echo "curl -X GET $API_URL/api/proxy/status -H 'Authorization: Bearer $TOKEN' | jq '.proxies[] | select(.isHealthy == true)'"
