# Database Migration - ScrapingJobId Kolonu Ekleme

## ⚠️ SORUN
Backend'de yeni `ScrapingJobId` kolonu eklendi ama PostgreSQL veritabanında bu kolon yok!

## ✅ ÇÖZÜM

### Seçenek 1: SQL ile Manuel Ekleme (HIZLI)

PostgreSQL'e bağlanın ve şu SQL'i çalıştırın:

```sql
-- ScrapingJobId kolonunu ekle
ALTER TABLE "Businesses" 
ADD COLUMN "ScrapingJobId" INTEGER NULL;

-- Foreign key constraint ekle (opsiyonel)
ALTER TABLE "Businesses"
ADD CONSTRAINT "FK_Businesses_ScrapingJobs_ScrapingJobId"
FOREIGN KEY ("ScrapingJobId") 
REFERENCES "ScrapingJobs"("Id")
ON DELETE SET NULL;

-- Index ekle (performans için)
CREATE INDEX "IX_Businesses_ScrapingJobId" 
ON "Businesses"("ScrapingJobId");
```

### Seçenek 2: EF Core Migration (ÖNERİLEN)

Terminal'de:

```bash
cd /Users/nuranguler/Desktop/TradeScout/FGS_APİ/TradeScout.API

# Migration oluştur
dotnet ef migrations add AddScrapingJobIdToBusiness

# Database'e uygula
dotnet ef database update
```

### Seçenek 3: Kolay Yol - Tabloyu Yeniden Oluştur

**DİKKAT: Bu yöntem mevcut verileri SİLER!**

```bash
# PostgreSQL'e bağlan
psql -h localhost -U postgres -d TradeScoutDb

# Tabloyu sil ve yeniden oluştur (Backend otomatik oluşturur)
DROP TABLE "Businesses" CASCADE;

# Backend'i yeniden başlat (tablo otomatik oluşacak)
```

## 🚀 HANGİSİNİ SEÇMELİ?

### Eğer PostgreSQL kurulu ve erişilebilirse:
✅ **Seçenek 1** - En hızlı, veriler korunur

### Eğer EF Core Migration kullanmak istiyorsanız:
✅ **Seçenek 2** - Professional yöntem, veriler korunur

### Eğer test ortamındaysanız ve veriler önemli değilse:
⚠️ **Seçenek 3** - En basit ama VERİLER SİLİNİR!

---

## 🔧 Şu An Yapılacaklar:

1. **PostgreSQL'e eriş**
2. **Seçenek 1'deki SQL'i çalıştır**
3. **Backend'i yeniden başlat** (zaten çalışıyor olabilir)
4. **Frontend'den tekrar dene**

---

**Backend hazır, sadece database kolonu eksik! SQL'i çalıştırın ve çalışacak!** 🚀
