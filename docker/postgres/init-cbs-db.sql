-- ==============================================================================
-- CBS Bangladesh Core Banking System - PostgreSQL 16 Production Init Script
-- Conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024)
-- ==============================================================================

-- Enable essential cryptographic and UUID extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Set standard timezone to Asia/Dhaka (+06:00)
ALTER DATABASE cbs_production SET timezone TO 'Asia/Dhaka';

-- Performance and safety locks for financial transactions
-- Ensure strict transaction isolation default (Read Committed)
ALTER DATABASE cbs_production SET default_transaction_isolation TO 'read committed';

-- Log long transactions exceeding 5 seconds (potential lock contention)
ALTER DATABASE cbs_production SET log_min_duration_statement = 5000;

-- Audit notice
DO $$
BEGIN
    RAISE NOTICE 'CBS Bangladesh Production Database initialized with extensions and Asia/Dhaka timezone.';
END $$;
