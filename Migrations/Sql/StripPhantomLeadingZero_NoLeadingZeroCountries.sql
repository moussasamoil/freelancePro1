-- One-shot data fix: remove the phantom leading "0" that NormalizePhone used to prepend
-- after stripping 00xxx/+xxx prefixes, for countries whose local subscriber format
-- does NOT use a leading 0 (Qatar, Oman, Bahrain, Tunisia, Kuwait).
--
-- Country IDs (Common.Countries enum):
--   3  = قطر          (Qatar)        local length 8, no leading 0
--   5  = سلطنة_عمان   (Oman)         local length 8, no leading 0
--   9  = الكويت       (Kuwait)       local length 8, no leading 0
--   10 = البحرين      (Bahrain)      local length 8, no leading 0
--   12 = تونس         (Tunisia)      local length 8, no leading 0
--
-- Safety guards:
--   - Strip only when value is 9 chars and starts with '0' (the exact malformed shape).
--   - Do not touch rows whose phone is already 8 chars (already correct).
--   - Do not touch values starting with '+' or '00' (those are pre-NormalizePhone legacy and
--     should be handled separately if they exist; this migration won't corrupt them).

-- =====================================================================================
-- PHASE 1 — SCOUTING (read-only): run these BEFORE the UPDATEs to understand the data.
-- =====================================================================================

-- Reusable mapping (inlined as CASE in each query below):
--   3 → Qatar, 5 → Oman, 9 → Kuwait, 10 → Bahrain, 12 → Tunisia

-- 1a. How many rows in the 5 target countries, total.
SELECT
    Country,
    CASE Country
        WHEN 3  THEN 'Qatar'
        WHEN 5  THEN 'Oman'
        WHEN 9  THEN 'Kuwait'
        WHEN 10 THEN 'Bahrain'
        WHEN 12 THEN 'Tunisia'
    END AS CountryName,
    COUNT(*) AS TotalRows
FROM Orders
WHERE Country IN (3, 5, 9, 10, 12)
GROUP BY Country
ORDER BY Country;

-- 1b. How many rows the UPDATE will actually change, per country and per column.
SELECT
    Country,
    CASE Country
        WHEN 3  THEN 'Qatar'
        WHEN 5  THEN 'Oman'
        WHEN 9  THEN 'Kuwait'
        WHEN 10 THEN 'Bahrain'
        WHEN 12 THEN 'Tunisia'
    END AS CountryName,
    SUM(CASE WHEN TelephoneNumber IS NOT NULL
              AND LEN(TelephoneNumber) = 9
              AND LEFT(TelephoneNumber, 1) = '0' THEN 1 ELSE 0 END) AS WillStrip_Telephone,
    SUM(CASE WHEN SecondTelephoneNumber IS NOT NULL
              AND LEN(SecondTelephoneNumber) = 9
              AND LEFT(SecondTelephoneNumber, 1) = '0' THEN 1 ELSE 0 END) AS WillStrip_SecondTelephone
FROM Orders
WHERE Country IN (3, 5, 9, 10, 12)
GROUP BY Country
ORDER BY Country;

-- 1c. Shape distribution of TelephoneNumber across the 5 countries — tells us if there
--     are unexpected formats (length != 8 or 9, non-digit chars, leading + or 00, etc.).
SELECT
    Country,
    CASE Country
        WHEN 3  THEN 'Qatar'
        WHEN 5  THEN 'Oman'
        WHEN 9  THEN 'Kuwait'
        WHEN 10 THEN 'Bahrain'
        WHEN 12 THEN 'Tunisia'
    END AS CountryName,
    LEN(TelephoneNumber) AS PhoneLength,
    CASE
        WHEN TelephoneNumber IS NULL                          THEN 'NULL'
        WHEN TelephoneNumber LIKE '+%'                        THEN 'starts with +'
        WHEN TelephoneNumber LIKE '00%'                       THEN 'starts with 00'
        WHEN LEFT(TelephoneNumber, 1) = '0'                   THEN 'starts with 0 (target)'
        WHEN TelephoneNumber LIKE '%[^0-9]%'                  THEN 'contains non-digit'
        ELSE                                                       'plain local digits'
    END AS Shape,
    COUNT(*) AS RowsFound
FROM Orders
WHERE Country IN (3, 5, 9, 10, 12)
GROUP BY Country,
         LEN(TelephoneNumber),
         CASE
             WHEN TelephoneNumber IS NULL                     THEN 'NULL'
             WHEN TelephoneNumber LIKE '+%'                   THEN 'starts with +'
             WHEN TelephoneNumber LIKE '00%'                  THEN 'starts with 00'
             WHEN LEFT(TelephoneNumber, 1) = '0'              THEN 'starts with 0 (target)'
             WHEN TelephoneNumber LIKE '%[^0-9]%'             THEN 'contains non-digit'
             ELSE                                                  'plain local digits'
         END
ORDER BY Country, PhoneLength;

-- 1d. Sample up to 1000 candidate rows showing before/after for both columns.
--     Eyeball this list to spot anything that looks wrong before committing.
SELECT TOP 1000
    Id,
    Country,
    CASE Country
        WHEN 3  THEN 'Qatar'
        WHEN 5  THEN 'Oman'
        WHEN 9  THEN 'Kuwait'
        WHEN 10 THEN 'Bahrain'
        WHEN 12 THEN 'Tunisia'
    END                                  AS CountryName,
    TelephoneNumber                      AS PrimaryBefore,
    CASE
        WHEN TelephoneNumber IS NOT NULL
         AND LEN(TelephoneNumber) = 9
         AND LEFT(TelephoneNumber, 1) = '0'
        THEN SUBSTRING(TelephoneNumber, 2, 8)
        ELSE TelephoneNumber
    END                                  AS PrimaryAfter,
    SecondTelephoneNumber                AS SecondBefore,
    CASE
        WHEN SecondTelephoneNumber IS NOT NULL
         AND LEN(SecondTelephoneNumber) = 9
         AND LEFT(SecondTelephoneNumber, 1) = '0'
        THEN SUBSTRING(SecondTelephoneNumber, 2, 8)
        ELSE SecondTelephoneNumber
    END                                  AS SecondAfter
FROM Orders
WHERE Country IN (3, 5, 9, 10, 12)
  AND (
        (TelephoneNumber       IS NOT NULL AND LEN(TelephoneNumber)       = 9 AND LEFT(TelephoneNumber, 1)       = '0')
     OR (SecondTelephoneNumber IS NOT NULL AND LEN(SecondTelephoneNumber) = 9 AND LEFT(SecondTelephoneNumber, 1) = '0')
      )
ORDER BY Country, Id;

-- 1e. Anomalies — rows in the 5 countries that DON'T match either the target shape (9 chars + leading 0)
--     or the already-correct shape (8 chars + no leading 0). These rows will be left untouched by the
--     UPDATE; inspect them to decide if they need a separate cleanup pass.
SELECT TOP 1000
    Id,
    Country,
    CASE Country
        WHEN 3  THEN 'Qatar'
        WHEN 5  THEN 'Oman'
        WHEN 9  THEN 'Kuwait'
        WHEN 10 THEN 'Bahrain'
        WHEN 12 THEN 'Tunisia'
    END AS CountryName,
    TelephoneNumber,
    SecondTelephoneNumber
FROM Orders
WHERE Country IN (3, 5, 9, 10, 12)
  AND (
        (TelephoneNumber IS NOT NULL
         AND NOT (LEN(TelephoneNumber) = 8 AND LEFT(TelephoneNumber, 1) <> '0')
         AND NOT (LEN(TelephoneNumber) = 9 AND LEFT(TelephoneNumber, 1) = '0'))
     OR (SecondTelephoneNumber IS NOT NULL
         AND NOT (LEN(SecondTelephoneNumber) = 8 AND LEFT(SecondTelephoneNumber, 1) <> '0')
         AND NOT (LEN(SecondTelephoneNumber) = 9 AND LEFT(SecondTelephoneNumber, 1) = '0'))
      )
ORDER BY Country, Id;

-- =====================================================================================
-- PHASE 2 — UPDATE (transactional): run only after Phase 1 looks correct.
-- =====================================================================================

BEGIN TRANSACTION;

-- Orders.TelephoneNumber
UPDATE Orders
SET TelephoneNumber = SUBSTRING(TelephoneNumber, 2, 8)
WHERE Country IN (3, 5, 9, 10, 12)
  AND TelephoneNumber IS NOT NULL
  AND LEN(TelephoneNumber) = 9
  AND LEFT(TelephoneNumber, 1) = '0';

-- Orders.SecondTelephoneNumber
UPDATE Orders
SET SecondTelephoneNumber = SUBSTRING(SecondTelephoneNumber, 2, 8)
WHERE Country IN (3, 5, 9, 10, 12)
  AND SecondTelephoneNumber IS NOT NULL
  AND LEN(SecondTelephoneNumber) = 9
  AND LEFT(SecondTelephoneNumber, 1) = '0';

-- Inspect before committing:
SELECT Id,
       Country,
       CASE Country
           WHEN 3  THEN 'Qatar'
           WHEN 5  THEN 'Oman'
           WHEN 9  THEN 'Kuwait'
           WHEN 10 THEN 'Bahrain'
           WHEN 12 THEN 'Tunisia'
       END AS CountryName,
       TelephoneNumber,
       SecondTelephoneNumber
FROM Orders
WHERE Country IN (3, 5, 9, 10, 12);

-- The UPDATEs above ran inside the open BEGIN TRANSACTION but are NOT yet persisted.
-- The COMMIT / ROLLBACK below are intentionally left commented so the transaction stays open
-- after the UPDATEs execute. Re-run the Phase 1 scouting queries (especially 1b) to verify the
-- row counts dropped to 0 and the data looks right, then manually uncomment + run ONE of:
--   COMMIT;     -- save the changes permanently
--   ROLLBACK;   -- discard everything, nothing was actually written
-- COMMIT;
-- ROLLBACK;
