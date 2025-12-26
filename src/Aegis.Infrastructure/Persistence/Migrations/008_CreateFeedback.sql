-- Migration: Create Feedback Table
-- Description: Stores user feedback on RAG query responses

CREATE TABLE IF NOT EXISTS feedback (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    query_history_id UUID NOT NULL REFERENCES query_history(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    type INTEGER NOT NULL, -- 1=Positive, 2=Negative, 3=Neutral
    comment TEXT,
    metadata JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    CONSTRAINT unique_feedback_per_query UNIQUE (query_history_id, user_id)
);

-- Indexes for efficient queries
CREATE INDEX IF NOT EXISTS idx_feedback_query_history ON feedback(query_history_id);
CREATE INDEX IF NOT EXISTS idx_feedback_workspace ON feedback(workspace_id);
CREATE INDEX IF NOT EXISTS idx_feedback_user ON feedback(user_id);
CREATE INDEX IF NOT EXISTS idx_feedback_type ON feedback(type);
CREATE INDEX IF NOT EXISTS idx_feedback_created_at ON feedback(created_at);

-- Index for full-text search on comments
CREATE INDEX IF NOT EXISTS idx_feedback_comment_search ON feedback USING gin(to_tsvector('english', COALESCE(comment, '')));

COMMENT ON TABLE feedback IS 'User feedback on RAG query responses';
COMMENT ON COLUMN feedback.type IS '1=Positive, 2=Negative, 3=Neutral';
COMMENT ON COLUMN feedback.metadata IS 'Additional metadata as JSON';
