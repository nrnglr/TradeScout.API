# Tosla Callback Sorunu - Kontrol Listesi

## ❌ SORUN
- Ödeme başarılı oluyor
- Para Tosla'ya geçiyor
- Ama backend'e callback gelmiyor
- Log'da hiç callback kaydı yok

## 🔍 Olası Nedenler

### 1. NGINX Yapılandırması
```bash
# NGINX config'i kontrol et
cat /etc/nginx/sites-available/api.fgstrade.com

# Aranacak kısım:
location /api/payment/callback {
    proxy_pass http://localhost:5100;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

### 2. Firewall/Security Group
```bash
# Port 80/443 dışarıdan erişilebilir mi?
sudo ufw status
sudo iptables -L -n
```

### 3. Tosla Panel Ayarları
- Tosla merchant panel → Settings → Callback URL
- Doğru URL: `https://api.fgstrade.com/api/payment/callback`
- Test/Canlı ortam ayrımı

### 4. SSL Sertifikası
```bash
# SSL geçerli mi?
curl -I https://api.fgstrade.com
openssl s_client -connect api.fgstrade.com:443 -servername api.fgstrade.com
```

## ✅ ÇÖZÜM ADIMLARı

### Adım 1: NGINX Config Düzelt
```nginx
server {
    listen 80;
    listen 443 ssl http2;
    server_name api.fgstrade.com;

    # SSL sertifikaları
    ssl_certificate /etc/letsencrypt/live/api.fgstrade.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api.fgstrade.com/privkey.pem;

    # Backend proxy
    location / {
        proxy_pass http://localhost:5100;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
        
        # CORS (Tosla callback için)
        add_header 'Access-Control-Allow-Origin' '*';
        add_header 'Access-Control-Allow-Methods' 'GET, POST, OPTIONS';
        add_header 'Access-Control-Allow-Headers' 'DNT,User-Agent,X-Requested-With,If-Modified-Since,Cache-Control,Content-Type,Range';
        
        # OPTIONS request için
        if ($request_method = 'OPTIONS') {
            return 204;
        }
    }
}
```

### Adım 2: NGINX Restart
```bash
sudo nginx -t
sudo systemctl restart nginx
```

### Adım 3: Backend'i Restart Et
```bash
pm2 restart backend
pm2 logs backend --lines 50
```

### Adım 4: Manuel Callback Testi
```bash
# Test script'i çalıştır
chmod +x test-callback.sh
./test-callback.sh

# Log'ları kontrol et
pm2 logs backend --lines 100 | grep -i callback
```

### Adım 5: Tosla Test
- Yeni bir test ödemesi yap
- Backend log'larını canlı izle: `pm2 logs backend --lines 0`
- Callback gelip gelmediğini gör

## 📝 Tespit Komutları

```bash
# 1. Backend çalışıyor mu?
curl http://localhost:5100/health

# 2. NGINX proxy çalışıyor mu?
curl -I https://api.fgstrade.com/health

# 3. Callback endpoint erişilebilir mi?
curl -X POST https://api.fgstrade.com/api/payment/callback \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "Code=0&OrderId=TEST123"

# 4. NGINX error log
sudo tail -f /var/log/nginx/error.log

# 5. Backend log (real-time)
pm2 logs backend --lines 0
```

## 🎯 En Muhtemel Sorun

**NGINX'te `/api/payment/callback` route'u yok veya yanlış yapılandırılmış!**

Çözüm:
1. NGINX config'e yukarıdaki `location /` bloğunu ekle
2. NGINX restart
3. Test et

## 💡 Hızlı Test

```bash
# Terminal 1: Backend log izle
pm2 logs backend --lines 0

# Terminal 2: Manuel callback gönder
curl -X POST https://api.fgstrade.com/api/payment/callback \
  -F "Code=0" \
  -F "BankResponseCode=00" \
  -F "OrderId=FGS26031214450000002" \
  -F "Amount=100" \
  -F "TransactionId=TEST123"

# Terminal 1'de "🔔 TOSLA CALLBACK START" görmelisin!
```
