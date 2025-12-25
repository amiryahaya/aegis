-- Add missing columns to workspaces table
ALTER TABLE workspaces ADD COLUMN IF NOT EXISTS created_by UUID REFERENCES users(id);
ALTER TABLE workspaces ADD COLUMN IF NOT EXISTS status VARCHAR(50) NOT NULL DEFAULT 'Active';

-- Make team_id nullable (workspaces can exist without a team)
ALTER TABLE workspaces ALTER COLUMN team_id DROP NOT NULL;

-- Create index for created_by
CREATE INDEX IF NOT EXISTS idx_workspaces_created_by ON workspaces(created_by);
