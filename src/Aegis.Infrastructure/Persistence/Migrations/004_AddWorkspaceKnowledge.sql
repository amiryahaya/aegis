-- Migration: Add Workspace Knowledge Base tables
-- Description: Curated entities, findings, and facts for workspace intelligence

-- Add custom_instructions to workspaces
ALTER TABLE workspaces ADD COLUMN IF NOT EXISTS custom_instructions TEXT;

-- Workspace Entities (curated entities in workspace knowledge base)
CREATE TABLE IF NOT EXISTS workspace_entities (
    id UUID PRIMARY KEY,
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    name VARCHAR(500) NOT NULL,
    type VARCHAR(100) NOT NULL, -- Person, Organization, Location, Event, etc.
    description TEXT,
    aliases JSONB NOT NULL DEFAULT '[]'::jsonb, -- Array of alias strings
    confidence VARCHAR(20) NOT NULL DEFAULT 'Medium', -- Low, Medium, High, Confirmed
    source_conversation_id UUID REFERENCES conversations(id) ON DELETE SET NULL,
    added_by UUID NOT NULL REFERENCES users(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_workspace_entities_workspace_id ON workspace_entities(workspace_id);
CREATE INDEX IF NOT EXISTS idx_workspace_entities_type ON workspace_entities(type);
CREATE INDEX IF NOT EXISTS idx_workspace_entities_name ON workspace_entities(name);
CREATE INDEX IF NOT EXISTS idx_workspace_entities_added_by ON workspace_entities(added_by);

-- Workspace Findings (key findings and hypotheses)
CREATE TABLE IF NOT EXISTS workspace_findings (
    id UUID PRIMARY KEY,
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    title VARCHAR(500) NOT NULL,
    content TEXT NOT NULL,
    type VARCHAR(50) NOT NULL DEFAULT 'Evidence', -- Evidence, Hypothesis, Conclusion, Pattern
    supporting_entity_ids JSONB NOT NULL DEFAULT '[]'::jsonb, -- Array of UUIDs
    source_document_ids JSONB NOT NULL DEFAULT '[]'::jsonb, -- Array of UUIDs
    source_conversation_id UUID REFERENCES conversations(id) ON DELETE SET NULL,
    added_by UUID NOT NULL REFERENCES users(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_workspace_findings_workspace_id ON workspace_findings(workspace_id);
CREATE INDEX IF NOT EXISTS idx_workspace_findings_type ON workspace_findings(type);
CREATE INDEX IF NOT EXISTS idx_workspace_findings_added_by ON workspace_findings(added_by);
CREATE INDEX IF NOT EXISTS idx_workspace_findings_created_at ON workspace_findings(created_at DESC);

-- Workspace Facts (established facts)
CREATE TABLE IF NOT EXISTS workspace_facts (
    id UUID PRIMARY KEY,
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    statement TEXT NOT NULL,
    confidence VARCHAR(20) NOT NULL DEFAULT 'Confirmed', -- Suspected, Likely, Confirmed, Verified
    source_document_ids JSONB NOT NULL DEFAULT '[]'::jsonb, -- Array of UUIDs
    source_conversation_id UUID REFERENCES conversations(id) ON DELETE SET NULL,
    added_by UUID NOT NULL REFERENCES users(id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_workspace_facts_workspace_id ON workspace_facts(workspace_id);
CREATE INDEX IF NOT EXISTS idx_workspace_facts_confidence ON workspace_facts(confidence);
CREATE INDEX IF NOT EXISTS idx_workspace_facts_added_by ON workspace_facts(added_by);
CREATE INDEX IF NOT EXISTS idx_workspace_facts_created_at ON workspace_facts(created_at DESC);
