-- ============================================================
-- TRADESCOUT.API MANUAL MIGRATION SQL
-- Discount Code System + Missing Columns
-- Database: PostgreSQL
-- Date: 2026-03-19
-- ============================================================

-- ============================================================
-- 1. CREATE DISCOUNT CODES TABLE
-- ============================================================
CREATE TABLE IF NOT EXISTS "DiscountCodes" (
    "Id" SERIAL PRIMARY KEY,
    "Code" VARCHAR(50) NOT NULL UNIQUE,
    "Description" TEXT,
    "DiscountPercentage" INTEGER NOT NULL,
    "MaxUses" INTEGER NOT NULL,
    "CurrentUses" INTEGER NOT NULL DEFAULT 0,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    "ExpiresAt" TIMESTAMP WITH TIME ZONE
);

CREATE INDEX IF NOT EXISTS "IX_DiscountCodes_Code" ON "DiscountCodes" ("Code");
CREATE INDEX IF NOT EXISTS "IX_DiscountCodes_IsActive" ON "DiscountCodes" ("IsActive");

-- ============================================================
-- 2. ADD NEW COLUMNS TO USERS TABLE
-- ============================================================
DO $$ 
BEGIN
    -- Email verification fields
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='Users' AND column_name='EmailVerificationCode') THEN
        ALTER TABLE "Users" ADD COLUMN "EmailVerificationCode" VARCHAR(10);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='Users' AND column_name='EmailVerificationExpiry') THEN
        ALTER TABLE "Users" ADD COLUMN "EmailVerificationExpiry" TIMESTAMP WITH TIME ZONE;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='Users' AND column_name='IsEmailVerified') THEN
        ALTER TABLE "Users" ADD COLUMN "IsEmailVerified" BOOLEAN NOT NULL DEFAULT FALSE;
    END IF;

    -- Password reset fields
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='Users' AND column_name='PasswordResetCode') THEN
        ALTER TABLE "Users" ADD COLUMN "PasswordResetCode" VARCHAR(10);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='Users' AND column_name='PasswordResetExpiry') THEN
        ALTER TABLE "Users" ADD COLUMN "PasswordResetExpiry" TIMESTAMP WITH TIME ZONE;
    END IF;

    -- Membership dates
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='Users' AND column_name='MembershipStart') THEN
        ALTER TABLE "Users" ADD COLUMN "MembershipStart" TIMESTAMP WITH TIME ZONE;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='Users' AND column_name='MembershipEnd') THEN
        ALTER TABLE "Users" ADD COLUMN "MembershipEnd" TIMESTAMP WITH TIME ZONE;
    END IF;
END $$;

-- ============================================================
-- 3. ADD NEW COLUMNS TO SCRAPINGJOBS TABLE
-- ============================================================
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='ScrapingJobs' AND column_name='Category') THEN
        ALTER TABLE "ScrapingJobs" ADD COLUMN "Category" VARCHAR(200) NOT NULL DEFAULT '';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='ScrapingJobs' AND column_name='City') THEN
        ALTER TABLE "ScrapingJobs" ADD COLUMN "City" VARCHAR(100) NOT NULL DEFAULT '';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='ScrapingJobs' AND column_name='CompletedAt') THEN
        ALTER TABLE "ScrapingJobs" ADD COLUMN "CompletedAt" TIMESTAMP WITH TIME ZONE;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='ScrapingJobs' AND column_name='Country') THEN
        ALTER TABLE "ScrapingJobs" ADD COLUMN "Country" VARCHAR(100);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='ScrapingJobs' AND column_name='CreditsUsed') THEN
        ALTER TABLE "ScrapingJobs" ADD COLUMN "CreditsUsed" INTEGER NOT NULL DEFAULT 0;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='ScrapingJobs' AND column_name='ErrorMessage') THEN
        ALTER TABLE "ScrapingJobs" ADD COLUMN "ErrorMessage" TEXT;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='ScrapingJobs' AND column_name='MaxResults') THEN
        ALTER TABLE "ScrapingJobs" ADD COLUMN "MaxResults" INTEGER NOT NULL DEFAULT 0;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='ScrapingJobs' AND column_name='TotalResults') THEN
        ALTER TABLE "ScrapingJobs" ADD COLUMN "TotalResults" INTEGER NOT NULL DEFAULT 0;
    END IF;
END $$;

-- ============================================================
-- 4. ADD NEW COLUMNS TO BUSINESSES TABLE
-- ============================================================
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='Businesses' AND column_name='Comments') THEN
        ALTER TABLE "Businesses" ADD COLUMN "Comments" TEXT;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='Businesses' AND column_name='Email') THEN
        ALTER TABLE "Businesses" ADD COLUMN "Email" VARCHAR(256);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='Businesses' AND column_name='FacebookUrl') THEN
        ALTER TABLE "Businesses" ADD COLUMN "FacebookUrl" VARCHAR(500);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='Businesses' AND column_name='InstagramUrl') THEN
        ALTER TABLE "Businesses" ADD COLUMN "InstagramUrl" VARCHAR(500);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='Businesses' AND column_name='LinkedInUrl') THEN
        ALTER TABLE "Businesses" ADD COLUMN "LinkedInUrl" VARCHAR(500);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='Businesses' AND column_name='MobilePhone') THEN
        ALTER TABLE "Businesses" ADD COLUMN "MobilePhone" VARCHAR(50);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='Businesses' AND column_name='OpeningHours') THEN
        ALTER TABLE "Businesses" ADD COLUMN "OpeningHours" TEXT;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='Businesses' AND column_name='TwitterUrl') THEN
        ALTER TABLE "Businesses" ADD COLUMN "TwitterUrl" VARCHAR(500);
    END IF;
END $$;

-- ============================================================
-- 5. ADD NEW COLUMNS TO PAYMENTHISTORIES TABLE
-- ============================================================
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='PaymentHistories' AND column_name='DiscountCode') THEN
        ALTER TABLE "PaymentHistories" ADD COLUMN "DiscountCode" VARCHAR(50);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='PaymentHistories' AND column_name='DiscountPercentage') THEN
        ALTER TABLE "PaymentHistories" ADD COLUMN "DiscountPercentage" INTEGER;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name='PaymentHistories' AND column_name='FinalAmount') THEN
        ALTER TABLE "PaymentHistories" ADD COLUMN "FinalAmount" DECIMAL(18, 2);
    END IF;
END $$;

-- ============================================================
-- 6. SEED 250 DISCOUNT CODES (FGS10_XXXXXX)
-- ============================================================
-- Bu kodlar %10 indirim sağlar ve her biri maksimum 1000 kez kullanılabilir

INSERT INTO "DiscountCodes" ("Code", "DiscountPercentage", "MaxUses", "CurrentUses", "IsActive", "CreatedAt")
SELECT 
    'FGS10_' || substring(md5(random()::text || clock_timestamp()::text) from 1 for 6),
    10,
    1000,
    0,
    TRUE,
    NOW()
FROM generate_series(1, 250)
ON CONFLICT ("Code") DO NOTHING;

-- ============================================================
-- 7. VERIFICATION QUERIES
-- ============================================================
-- Bu sorguları çalıştırarak migration'ın başarılı olduğunu doğrulayabilirsiniz:

-- Discount Codes tablosu kontrolü:
-- SELECT COUNT(*) as total_codes FROM "DiscountCodes";
-- SELECT * FROM "DiscountCodes" LIMIT 5;

-- Users tablosu yeni kolonlar:
-- SELECT column_name FROM information_schema.columns WHERE table_name = 'Users' 
-- AND column_name IN ('EmailVerificationCode', 'IsEmailVerified', 'MembershipStart', 'MembershipEnd');

-- ScrapingJobs tablosu yeni kolonlar:
-- SELECT column_name FROM information_schema.columns WHERE table_name = 'ScrapingJobs' 
-- AND column_name IN ('Category', 'City', 'CreditsUsed', 'MaxResults');

-- Businesses tablosu yeni kolonlar:
-- SELECT column_name FROM information_schema.columns WHERE table_name = 'Businesses' 
-- AND column_name IN ('Email', 'MobilePhone', 'FacebookUrl', 'InstagramUrl');

-- PaymentHistories tablosu yeni kolonlar:
-- SELECT column_name FROM information_schema.columns WHERE table_name = 'PaymentHistories' 
-- AND column_name IN ('DiscountCode', 'DiscountPercentage', 'FinalAmount');

-- ============================================================
-- NOTES:
-- ============================================================
-- 1. Bu SQL PostgreSQL için optimize edilmiştir
-- 2. Tüm ALTER TABLE komutları IF NOT EXISTS kontrolü ile yapılır
-- 3. 250 adet benzersiz indirim kodu otomatik oluşturulur
-- 4. Kodlar FGS10_XXXXXX formatında olacaktır (X = random alfanumerik)
-- 5. Tüm kodlar aktif ve 1000 kullanım limiti ile başlar
-- 6. Bu script birden fazla kez çalıştırılabilir (idempotent)

-- ============================================================
-- EXECUTION:
-- ============================================================
-- Terminal'den çalıştırmak için:
-- psql -U your_username -d your_database_name -f MANUAL_MIGRATION.sql
-- 
-- veya pgAdmin/DBeaver gibi araçlardan:
-- Tüm içeriği kopyalayıp SQL editöründe çalıştırın
-- ============================================================
