-- =========================================
-- CANLI VERİTABANI İÇİN UTC DATETIME CONVERTER MİGRATION
-- Migration: 20260324161530_AddUtcDateTimeConverter
-- Tarih: 24 Mart 2026
-- =========================================
-- 
-- Bu script, tüm DateTime kolonlarını "timestamp with time zone" tipinden
-- "timestamp without time zone" tipine çevirir.
-- 
-- ÖNEMLİ: Bu işlem GERİ DÖNDÜRÜLEMEZ! Önce yedek alın!
-- 
-- KULLANIM:
-- 1. Veritabanınızın yedeğini alın
-- 2. pgAdmin, psql veya başka bir PostgreSQL client ile bağlanın
-- 3. Bu scripti çalıştırın
-- =========================================

BEGIN;

-- =========================================
-- USERS TABLOSU
-- =========================================
ALTER TABLE "Users" 
    ALTER COLUMN "PasswordResetExpiry" TYPE timestamp without time zone;

ALTER TABLE "Users" 
    ALTER COLUMN "MembershipStart" TYPE timestamp without time zone;

ALTER TABLE "Users" 
    ALTER COLUMN "MembershipEnd" TYPE timestamp without time zone;

ALTER TABLE "Users" 
    ALTER COLUMN "LastLogin" TYPE timestamp without time zone;

ALTER TABLE "Users" 
    ALTER COLUMN "EmailVerificationExpiry" TYPE timestamp without time zone;

ALTER TABLE "Users" 
    ALTER COLUMN "CreatedAt" TYPE timestamp without time zone;

-- =========================================
-- SCRAPINGJOBS TABLOSU
-- =========================================
ALTER TABLE "ScrapingJobs" 
    ALTER COLUMN "StartedAt" TYPE timestamp without time zone;

ALTER TABLE "ScrapingJobs" 
    ALTER COLUMN "CreatedAt" TYPE timestamp without time zone;

ALTER TABLE "ScrapingJobs" 
    ALTER COLUMN "CompletedAt" TYPE timestamp without time zone;

-- =========================================
-- PAYMENTHISTORIES TABLOSU
-- =========================================
ALTER TABLE "PaymentHistories" 
    ALTER COLUMN "PaymentDate" TYPE timestamp without time zone;

ALTER TABLE "PaymentHistories" 
    ALTER COLUMN "CreatedAt" TYPE timestamp without time zone;

-- =========================================
-- MARKETANALYSES TABLOSU
-- =========================================
ALTER TABLE "MarketAnalyses" 
    ALTER COLUMN "PdfDownloadedAt" TYPE timestamp without time zone;

ALTER TABLE "MarketAnalyses" 
    ALTER COLUMN "CreatedAt" TYPE timestamp without time zone;

-- =========================================
-- FEEDBACKS TABLOSU
-- =========================================
ALTER TABLE "Feedbacks" 
    ALTER COLUMN "RepliedAt" TYPE timestamp without time zone;

ALTER TABLE "Feedbacks" 
    ALTER COLUMN "CreatedAt" TYPE timestamp without time zone;

-- =========================================
-- DISCOUNTCODES TABLOSU
-- =========================================
ALTER TABLE "DiscountCodes" 
    ALTER COLUMN "ExpiresAt" TYPE timestamp without time zone;

ALTER TABLE "DiscountCodes" 
    ALTER COLUMN "CreatedAt" TYPE timestamp without time zone;

-- =========================================
-- BUSINESSES TABLOSU
-- =========================================
ALTER TABLE "Businesses" 
    ALTER COLUMN "CreatedAt" TYPE timestamp without time zone;

-- =========================================
-- MİGRATİON TABLOSUNA KAYIT EKLE
-- =========================================
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260324161530_AddUtcDateTimeConverter', '9.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;

-- =========================================
-- İŞLEM TAMAMLANDI
-- =========================================
-- Artık backend'inizdeki UTC datetime converter çalışacaktır.
-- Test için bir kullanıcı kaydı oluşturup CreatedAt alanını kontrol edin.
-- =========================================
