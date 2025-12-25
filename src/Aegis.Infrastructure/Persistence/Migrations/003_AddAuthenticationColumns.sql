-- Add authentication columns to users table
ALTER TABLE users
ADD COLUMN IF NOT EXISTS password_hash VARCHAR(255),
ADD COLUMN IF NOT EXISTS email_verified BOOLEAN NOT NULL DEFAULT false,
ADD COLUMN IF NOT EXISTS last_login_at TIMESTAMPTZ,
ADD COLUMN IF NOT EXISTS failed_login_attempts INTEGER NOT NULL DEFAULT 0,
ADD COLUMN IF NOT EXISTS lockout_until TIMESTAMPTZ;

-- Create index for lockout queries
CREATE INDEX IF NOT EXISTS idx_users_lockout_until ON users(lockout_until) WHERE lockout_until IS NOT NULL;
