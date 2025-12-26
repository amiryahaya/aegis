-- Migration: Enable UUID v7 Support
-- Description: Adds uuid-ossp extension and creates uuid_generate_v7() function for time-ordered UUIDs

-- PostgreSQL 18 doesn't have native UUID v7 yet, so we implement it using uuid-ossp and a custom function
-- This provides time-ordered, database-friendly UUIDs that improve index performance

-- Enable uuid-ossp extension for UUID generation functions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Create uuid_generate_v7() function
-- UUID v7 format: timestamp (48 bits) + version (4 bits) + random (12 bits) + variant (2 bits) + random (62 bits)
CREATE OR REPLACE FUNCTION uuid_generate_v7()
RETURNS uuid
AS $$
DECLARE
    -- Unix epoch in microseconds (48 bits for milliseconds, we'll use lower precision)
    unix_ts_ms bigint;
    -- Random bytes for the rest
    rand_a bytea;
    rand_b bytea;
    -- Final UUID parts
    time_part bytea;
    version_and_rand bytea;
    variant_and_rand bytea;
BEGIN
    -- Get current timestamp in milliseconds since Unix epoch
    unix_ts_ms := (EXTRACT(EPOCH FROM clock_timestamp()) * 1000)::bigint;

    -- Generate random bytes
    rand_a := gen_random_bytes(2);  -- 16 bits
    rand_b := gen_random_bytes(8);  -- 64 bits

    -- Build UUID v7
    -- Timestamp (48 bits = 6 bytes)
    time_part := substring(int8send(unix_ts_ms) from 3 for 6);

    -- Version nibble (4 bits) + 12 random bits = 2 bytes
    version_and_rand := set_byte(rand_a, 0, (get_byte(rand_a, 0) & 15) | 112); -- Version 7 (0x70)

    -- Variant (2 bits) + 62 random bits = 8 bytes
    variant_and_rand := set_byte(rand_b, 0, (get_byte(rand_b, 0) & 63) | 128); -- Variant 10 (0x80)

    -- Combine all parts into UUID
    RETURN encode(time_part || version_and_rand || variant_and_rand, 'hex')::uuid;
END;
$$ LANGUAGE plpgsql VOLATILE;

COMMENT ON FUNCTION uuid_generate_v7() IS 'Generates time-ordered UUID v7 (RFC 4122) - better for database performance than v4';

-- Test the function
DO $$
DECLARE
    test_uuid uuid;
BEGIN
    test_uuid := uuid_generate_v7();
    RAISE NOTICE 'Generated UUID v7: %', test_uuid;

    -- Verify it's a valid UUID
    IF test_uuid IS NULL THEN
        RAISE EXCEPTION 'uuid_generate_v7() returned NULL';
    END IF;
END;
$$;
